using System.Collections.Generic;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    public interface IProtocolScanner
    {
        string Name { get; }
        List<BatteryDevice> Scan();
    }
}
