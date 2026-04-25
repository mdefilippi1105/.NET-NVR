

using System.ServiceModel;
using System.Xml;
using Onvif.Core.Client;
using Rtsp.Sdp;
using Onvif.Core.Client.Media;
using OnvifDiscovery;

namespace VideoRecorder.Services;

public class AltOnvifDiscovery
{
    /*
     * The purpose of this class is to avoid hardcoding RTSP URL values
     * Discover the cam on the network via ONVIF
     * Ask each cam for the RTSP URL through the ONVIF media service
     * Feed the URL into StreamListener to connect
     */

    public List<string> rtspList = new List<string>();
    public List<string> onvifList = new List<string>();
    
    
    public async Task DiscoverAsync()
    {
        var discovery = new Discovery();
        var cancellationToken = new CancellationTokenSource().Token;
        
        await foreach (var device in discovery.DiscoverAsync(5, cancellationToken))
        {
            Console.WriteLine($"Found: {device.Mfr} {device.Model} at {device.Address}. Time is {DateTime.Now}");
            Console.WriteLine($"XAddress : {device.XAddresses.First()}");
            
            try
            {
                //create the Media client. this includes SOAP binding,
                //gets the media service URL by requesting cams capabilities
                //create the end point
                var media = await OnvifClientFactory.CreateMediaClientAsync(device.Address, "root", "pass");
            
                //ask cam for list of stream profiles
                var profilesResponse = await media.GetProfilesAsync();
            
                //grab the token - an id you pass to GetStreamUri - "give me the URL for this stream"
                string token = profilesResponse.Profiles[0].token;
                Console.WriteLine($"Token: {token}");
                onvifList.Add($"Camera: {token}");
            
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("ONVIF FAILURE");
                string rtspUrl = $"rtsp://{device.Address}:554/axis-media/media.amp";
                Console.WriteLine($"RTSP URL: {rtspUrl}");
                rtspList.Add(rtspUrl);
            }
        }
        Console.WriteLine($"Number of Onvif: {onvifList.Count}");
        Console.WriteLine($"Number of RTSP: {rtspList.Count}");
    }
}