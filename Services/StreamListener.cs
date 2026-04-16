namespace VideoRecorder.Services;

using Rtsp;
using Rtsp.Messages;



public class StreamListener
{
    // these are nullable by default
    private RtspTcpTransport? _tcpSocket;
    private RtspListener? _rtspListener;
    private string? _url;
    private string? _username;
    private string? _password;
    private bool _attemptedAuth = false;
    
    /************************************************************
     * This is a console version that ties all of these together.
     * At some point, if all testing is passed, I will try
     * to implement this on my videorecorder engine
     ************************************************************/
    public void RunStreaming()
    {
        Console.WriteLine("Please enter an ip address: ");
        string? host = Console.ReadLine();
        Console.WriteLine("Please enter an username: ");
        var username = Console.ReadLine();
        Console.WriteLine("Please enter a password: ");
        var password = Console.ReadLine();
        Console.WriteLine("Please enter camera manufacturer: ");
        var cameraManufacturer = Console.ReadLine();
        if (cameraManufacturer == "axis" || cameraManufacturer == "AXIS" || cameraManufacturer == "Axis")
        {
            var port = 554;
            var path = "/axis-media/media.amp";
            var s = new StreamListener();
            s.ClientConnect(host, port, path , username, password);
            s.StartListening();
            s.SendOptions();
            s.SendDescription();
            s.SendSetup();
            Console.ReadLine();
        }
        else
        {
            Console.WriteLine("This camera is unsupported.");
            Console.ReadLine();
        }
        Console.WriteLine("Press any key to exit.");
        Console.ReadLine();
    }
    
    /**********************************************************
     * create a new socket that we connect to
     * this would be considered the rtsp server
     * in this case, it would be an ip cam
     **********************************************************/
    
    //socket to rtsp
    public void ClientConnect(string host, int port, string path, string username, string password)
    {
        _username = username;
        _password = password;
        _url = $"rtsp://{host}:{port}/{path}";
        _tcpSocket = new RtspTcpTransport(new Uri(_url));

       if (!_tcpSocket.Connected)
       {
           Console.WriteLine("Client not connected");
           return;
       }
       Console.WriteLine("Client connected");
    }
    
    
    /*********************************************************
     * create the rtsp listener and attach it to the tcp socket
     * from ClientConnect().
     *
     **********************************************************/
    public void StartListening()
    {
        if (_tcpSocket == null) 
            return;
        _rtspListener = new RtspListener(_tcpSocket); // this is the object that will handle all read/write rtsp msg
        _rtspListener.MessageReceived += OnMessageReceived; //event handler
        _rtspListener.Start(); // start background thread

        Console.WriteLine("Listener started");
    }
    
    
    /*********************************************************
     * this method is called when cam sends back an RTSP response
     * here, we read the cameras reply and decide what to do next
     * will add more switch parameters and response codes
     **********************************************************/

    public void OnMessageReceived(object? sender, RtspChunkEventArgs e)
    {
        if (e.Message is not RtspResponse response) // make sure what we get back is an actual response
            return;

        Console.WriteLine($"Response: {response.ReturnCode} to {response.OriginalRequest}"); //may want to comment out the second half

        switch (response.ReturnCode)
        {
            case 401:
                Console.WriteLine("You are not authorized to use this command");
                HandleUnauthorize(response);
                return;
            case 200:
                HandleOk(response);
                return;
        }
    }

    /**********************************************************
     * this is for when the camera sends back a 200 - OK
     * switch -> if OK -> Send DESCRIBE
     * if OK to DESCRIBE -> Print SDP (stream info)
     **********************************************************/
    
    public void HandleOk(RtspResponse response)
    {
        switch (response.OriginalRequest)
        {
            case RtspRequestOptions:
                SendDescription();
                break;
            case RtspRequestDescribe:
                Console.WriteLine("I got sdp:");
                
                if (response.Data.Length > 0)
                    Console.WriteLine(System.Text.Encoding.UTF8.GetString(response.Data.ToArray()));
                break;
        }
    }
    
    
    /**********************************************************
     * Handling unauthorized events
     * 
     **********************************************************/
    public void HandleUnauthorize(RtspResponse response)
    {
        if (_attemptedAuth) // this is false by default
        {
            Console.WriteLine("Auth failed - please check username and password");
            return;
        }
        _attemptedAuth = true;
        
        if (response.OriginalRequest == null)
            return;
        
        // make a copy of original request. we do not want to modify the original,
        // and need a fresh object to add the auth header to.
        var retry = response.OriginalRequest.Clone() as RtspRequest;
        if (retry == null)
            return;
        
        // building a basic auth header. we need to convert user/pass
        // into array of bytes. we then convert the string of numbers
        // into Base64 string. this is safe for a HTTP header
        var creds = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"));
        
        retry.Headers["Authorization"] = $"Basic {creds}"; // add the headers
        
        _rtspListener.SendMessage(retry); // takes care of the rest aka convert to bytes and send down the line
        
    }
    
    /**********************************************************
     * creates rtspOptions request object. asks camera what
     * RTSP options are supported.
     **********************************************************/
    public void SendOptions()
    { 
        if (_rtspListener == null)
            return;
        
        var options = new RtspRequestOptions();
        options.RtspUri = new Uri(_url!);
        _rtspListener.SendMessage(options);

        Console.WriteLine($"Sent message: {_url}");
    }
    
    
    /**********************************************************
     * Send Description
     **********************************************************/
    public void SendDescription()
    {
        if (_rtspListener == null)
            return;
        var describe = new RtspRequestDescribe();
        describe.RtspUri  = new Uri(_url!);
        _rtspListener.SendMessage(describe);
        
        Console.WriteLine($"Sent description: {_url}");
    }
    
    /**********************************************************
     * Send Options
     * the variable control is from parsing the 
     **********************************************************/
    public void SendSetup(string controlUrl)
    {
        {
            if (_rtspListener == null)
                return;
            var setup = new RtspRequestSetup();
            setup.RtspUri = new Uri(controlUrl);
        
            // very important. tells the camera how you want to receive the video
            // rtp/avp/tcp - send video over same tcp connection; unicast - no broadcast
            // interleaved01 - video channel 0, control signals channel 1
            setup.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";
        
            _rtspListener.SendMessage(setup);
        
            Console.WriteLine($"Sent setup: {_url}");
        }
    }
    
    
    
    
    

    
    
    
}
}