using System.ServiceModel;
using System.Xml;
using Onvif.Core.Client;
using Onvif.Core.Client.Common;
using Rtsp.Sdp;
using Onvif.Core.Client.Media;
using OnvifDiscovery;
using DateTime = System.DateTime;

namespace VideoRecorder.Services;

public class AltOnvifDiscovery
{
    /*
     * The purpose of this class is to avoid hardcoding RTSP URL values
     * Discover the cam on the network via ONVIF
     * Ask each cam for the RTSP URL through the ONVIF media service
     * Feed the URL into StreamListener to connect
     */

    public List<string>? OnvifUriList { get; set; } = new();
    
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
                
                
                //set up the stream object with these params
                var streamSetup = new StreamSetup()
                {
                    Stream = StreamType.RTPUnicast,
                    Transport = new Transport { Protocol = TransportProtocol.RTSP }
                };
                //
                var streamUri = await media.GetStreamUriAsync(streamSetup, token);
                string rtspUrl = streamUri.Uri;
                Console.WriteLine($"StreamUri: {rtspUrl}");
                OnvifUriList?.Add(rtspUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}"); ;
            }

            Console.WriteLine($"TOTAL: {OnvifUriList?.Count}");
            
        }
    }
    
    
    
}