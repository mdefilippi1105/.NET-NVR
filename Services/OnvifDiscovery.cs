using System.Collections.ObjectModel;
using Onvif.Core.Client;
using Onvif.Core.Discovery;
using Onvif.Core.Discovery.Models;

namespace VideoRecorder.Services;

public class OnvifDiscovery
{
    public async Task< IEnumerable <DiscoveryDevice>> DiscoverAsync()
    {
        Console.WriteLine("Starting Discovery...");
        var discoveryService = new DiscoveryService();
        await discoveryService.Start(2000,  3702);
        
        var devices = discoveryService.DiscoveredDevices;
        
        Console.WriteLine($"Discovered {devices.Count} devices");
        
        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine("Found device: " + devices[i].Name);
        }
        return devices;
    }
}