
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VideoRecorder.Network;

public class UdpDiscoveryTools {
    private static UdpClient socket;
    private static int localPort;

     private static int Start()
    {
        socket = new UdpClient(0);
        
        if (socket.Client.LocalEndPoint is IPEndPoint endPoint)
        {
            localPort = endPoint.Port;
        }
        
        return localPort;
    }

    public static void Stop() 
    {
        // make sure actually points to a socket and make sure socket not already closed
        if (socket != null) 
        {
            socket.Close();
        }
    }

    public static void DisplayCurrentPort() {
        try
        {
            var port = UdpDiscoveryTools.Start();
            Console.WriteLine(" UDP ON PORT: " + port);
            UdpDiscoveryTools.Stop();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        
    }

    /************************************************************************
     *  Udp Discovery - start() creates the udp socket and returns
     * listening port number. the probe is a WS-Discovery message
     * XML that requests network video devices
     ************************************************************************/
    
    public static void SendDiscovery()
    {
        int port = Start();

        const string probe = """
                            <?xml version=""1.0"" encoding=""utf-8""?>
                            <Envelope xmlns=""http://www.w3.org/2003/05/soap-envelope"">
                                <Body>
                                    <Probe xmlns=""http://schemas.xmlsoap.org/ws/2005/04/discovery"">
                                        <Types>dn:NetworkVideoTransmitter</Types>
                                    </Probe>
                                </Body>
                            </Envelope>
                            """;
        
        byte[] data = Encoding.UTF8.GetBytes(probe);
        
        // we are sending 5 discovery packets. we can change this later if need be.
        for (int i = 0; i < 5; i++)
        {
            socket.Send(data, data.Length, "239.255.255.250", 3702);
            Thread.Sleep(500);
        }
        
    }

    public static List<string> ReceiveResponse()
    {
        var results = new List<string>();
        socket.Client.ReceiveTimeout = 10000; // this is 10 seconds, it may have to be adjusted
        try
        {
            while (true)
            {
                var endpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] reply = socket.Receive(ref endpoint);
                var ipString = endpoint.Address.ToString();
                
                //if the list has the string already don't re-add as a duplicate
                if (!results.Contains(ipString))
                {
                    Console.WriteLine(ipString.Length + " : " + ipString);
                    results.Add(ipString);
                }
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine(e.Message);
        }
        return results;
    }
    
    
}
