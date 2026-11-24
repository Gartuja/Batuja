using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading;
using OmniBattery.Models;

namespace OmniBattery.Core
{
    /// <summary>
    /// Gerenciador de toques de áudio para alertas de bateria fraca.
    /// Gera áudios WAV puros em memória de 1 a 3 segundos (sem arquivos externos ou dependências adicionais).
    /// Permite testar/ouvir nas Configurações antes de selecionar.
    /// </summary>
    public static class SoundAlertManager
    {
        private static readonly Dictionary<LowBatterySoundType, byte[]> _cache = new Dictionary<LowBatterySoundType, byte[]>();
        private static readonly object _lock = new object();

        public static string GetSoundLabel(LowBatterySoundType type)
        {
            switch (type)
            {
                case LowBatterySoundType.BipRetro:
                    return "Bip Retrô (8-Bit)";
                case LowBatterySoundType.SinoSuave:
                    return "Sino Suave (Chime)";
                case LowBatterySoundType.AlertaGamer:
                    return "Alerta Gamer (Eletrônico)";
                case LowBatterySoundType.RadarPixel:
                    return "Radar Pixel (Sonar)";
                case LowBatterySoundType.Desativado:
                    return "Desativado (Silencioso)";
                default:
                    return type.ToString();
            }
        }

        public static void PlaySound(LowBatterySoundType type)
        {
            if (type == LowBatterySoundType.Desativado)
                return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    byte[] wav = GetSoundBytes(type);
                    if (wav != null && wav.Length > 0)
                    {
                        using (MemoryStream ms = new MemoryStream(wav))
                        {
                            using (SoundPlayer player = new SoundPlayer(ms))
                            {
                                player.PlaySync();
                            }
                        }
                    }
                }
                catch { }
            });
        }

        public static byte[] GetSoundBytes(LowBatterySoundType type)
        {
            lock (_lock)
            {
                if (_cache.ContainsKey(type))
                    return _cache[type];

                byte[] generated = null;
                switch (type)
                {
                    case LowBatterySoundType.BipRetro:
                        generated = GenerateBipRetro();
                        break;
                    case LowBatterySoundType.SinoSuave:
                        generated = GenerateSinoSuave();
                        break;
                    case LowBatterySoundType.AlertaGamer:
                        generated = GenerateAlertaGamer();
                        break;
                    case LowBatterySoundType.RadarPixel:
                        generated = GenerateRadarPixel();
                        break;
                }

                if (generated != null)
                {
                    _cache[type] = generated;
                }

                return generated;
            }
        }

        private static byte[] WrapWavHeader(byte[] pcmData, int sampleRate)
        {
            byte[] wav = new byte[44 + pcmData.Length];
            int totalDataLen = pcmData.Length;
            int totalFileLen = totalDataLen + 36;
            int channels = 1;
            int byteRate = sampleRate * channels * 2;

            // RIFF header
            wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
            wav[4] = (byte)(totalFileLen & 0xff);
            wav[5] = (byte)((totalFileLen >> 8) & 0xff);
            wav[6] = (byte)((totalFileLen >> 16) & 0xff);
            wav[7] = (byte)((totalFileLen >> 24) & 0xff);
            wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';

            // 'fmt ' chunk
            wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
            wav[16] = 16; wav[17] = 0; wav[18] = 0; wav[19] = 0; // Subchunk size (16 for PCM)
            wav[20] = 1; wav[21] = 0; // PCM format = 1
            wav[22] = (byte)channels; wav[23] = 0;
            wav[24] = (byte)(sampleRate & 0xff);
            wav[25] = (byte)((sampleRate >> 8) & 0xff);
            wav[26] = (byte)((sampleRate >> 16) & 0xff);
            wav[27] = (byte)((sampleRate >> 24) & 0xff);
            wav[28] = (byte)(byteRate & 0xff);
            wav[29] = (byte)((byteRate >> 8) & 0xff);
            wav[30] = (byte)((byteRate >> 16) & 0xff);
            wav[31] = (byte)((byteRate >> 24) & 0xff);
            wav[32] = (byte)(channels * 2); wav[33] = 0; // Block align
            wav[34] = 16; wav[35] = 0; // Bits per sample

            // 'data' chunk
            wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
            wav[40] = (byte)(totalDataLen & 0xff);
            wav[41] = (byte)((totalDataLen >> 8) & 0xff);
            wav[42] = (byte)((totalDataLen >> 16) & 0xff);
            wav[43] = (byte)((totalDataLen >> 24) & 0xff);

            Buffer.BlockCopy(pcmData, 0, wav, 44, pcmData.Length);
            return wav;
        }

        // 1. Bip Retrô (8-Bit Arcade, ~1.2s)
        private static byte[] GenerateBipRetro()
        {
            int sampleRate = 22050;
            double duration = 1.25;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double sample = 0;

                if (t >= 0.0 && t < 0.18)
                {
                    double env = 1.0 - (t / 0.18) * 0.35;
                    double freq = 784.0; // G5
                    double sq = Math.Sin(2.0 * Math.PI * freq * t) > 0 ? 0.35 : -0.35;
                    sample = (Math.Sin(2.0 * Math.PI * freq * t) * 0.65 + sq) * env;
                }
                else if (t >= 0.24 && t < 0.65)
                {
                    double subT = t - 0.24;
                    double env = Math.Exp(-subT * 4.2);
                    double freq = 523.25; // C5
                    double sq = Math.Sin(2.0 * Math.PI * freq * subT) > 0 ? 0.3 : -0.3;
                    sample = (Math.Sin(2.0 * Math.PI * freq * subT) * 0.7 + sq) * env;
                }

                samples[i] = (short)(Math.Max(-1.0, Math.Min(1.0, sample)) * 26000);
            }

            byte[] pcm = new byte[totalSamples * 2];
            Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
            return WrapWavHeader(pcm, sampleRate);
        }

        // 2. Sino Suave (Chime Harmônico, ~2.0s)
        private static byte[] GenerateSinoSuave()
        {
            int sampleRate = 22050;
            double duration = 2.0;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Exp(-t * 2.2);

                double s1 = Math.Sin(2.0 * Math.PI * 659.25 * t);         // E5
                double s2 = Math.Sin(2.0 * Math.PI * 1318.5 * t) * 0.45;   // E6
                double s3 = Math.Sin(2.0 * Math.PI * 1977.0 * t) * 0.15;   // B6
                double sample = (s1 + s2 + s3) * env * 0.8;

                samples[i] = (short)(Math.Max(-1.0, Math.Min(1.0, sample)) * 26000);
            }

            byte[] pcm = new byte[totalSamples * 2];
            Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
            return WrapWavHeader(pcm, sampleRate);
        }

        // 3. Alerta Gamer (Eletrônico Futurista, ~1.4s)
        private static byte[] GenerateAlertaGamer()
        {
            int sampleRate = 22050;
            double duration = 1.45;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double sample = 0;

                if (t >= 0.0 && t < 0.12)
                {
                    double env = 1.0 - (t / 0.12) * 0.2;
                    sample = Math.Sin(2.0 * Math.PI * 523.25 * t) * env; // C5
                }
                else if (t >= 0.12 && t < 0.24)
                {
                    double subT = t - 0.12;
                    double env = 1.0 - (subT / 0.12) * 0.2;
                    sample = Math.Sin(2.0 * Math.PI * 659.25 * subT) * env; // E5
                }
                else if (t >= 0.24 && t < 0.95)
                {
                    double subT = t - 0.24;
                    double env = Math.Exp(-subT * 3.6);
                    double s1 = Math.Sin(2.0 * Math.PI * 1046.50 * subT); // C6
                    double s2 = Math.Sin(2.0 * Math.PI * 2093.00 * subT) * 0.3; // C7
                    sample = (s1 + s2) * env;
                }

                samples[i] = (short)(Math.Max(-1.0, Math.Min(1.0, sample)) * 26000);
            }

            byte[] pcm = new byte[totalSamples * 2];
            Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
            return WrapWavHeader(pcm, sampleRate);
        }

        // 4. Radar Pixel (Sonar Tático Sutil, ~1.3s)
        private static byte[] GenerateRadarPixel()
        {
            int sampleRate = 22050;
            double duration = 1.35;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double sample = 0;

                if (t >= 0.0 && t < 0.22)
                {
                    double env = Math.Exp(-t * 9.0);
                    sample = Math.Sin(2.0 * Math.PI * 1175.0 * t) * env; // D6
                }
                else if (t >= 0.26 && t < 0.70)
                {
                    double subT = t - 0.26;
                    double env = Math.Exp(-subT * 5.5);
                    sample = Math.Sin(2.0 * Math.PI * 1397.0 * subT) * env; // F6
                }

                samples[i] = (short)(Math.Max(-1.0, Math.Min(1.0, sample)) * 26000);
            }

            byte[] pcm = new byte[totalSamples * 2];
            Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
            return WrapWavHeader(pcm, sampleRate);
        }
    }
}
