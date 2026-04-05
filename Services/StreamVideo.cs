namespace VideoRecorder.Services;
using System.Diagnostics;
using OpenCvSharp;



/************************************************************************
 * can do this 2 ways: opencv or ffmpeg
 * use Process to use another program
 *
 * update: after reading, may want to use
 * WebRTC 
 ************************************************************************/



// get the ffmpeg data of whatever link you add
public class StreamVideo
{
    private int _processCounter;
    private bool _isMtxRunning;
    private bool _isFfmpegRunning;
    public bool StreamDataTest(string filename, Guid cameraId)
    {
            Process fProcess = new Process();
            // verbose logs for seeing everything, rtsp transport over tcp, point to the rtsp address
            fProcess.StartInfo.FileName = "/Users/michaeldefilippi/RiderProjects/VideoRecorder/VideoRecorder/Services/ffmpeg";
            fProcess.StartInfo.Arguments = $"-hide_banner -loglevel verbose" +
                                           $" -analyzeduration 10M -probesize 10M " +
                                           $"-rtsp_transport tcp -i \"{filename}\" " +
                                           $"-c:v copy -f rtsp " +
                                           $"rtsp://localhost:8554/live/{cameraId}";            
            fProcess.StartInfo.RedirectStandardError = true;
            fProcess.StartInfo.UseShellExecute = false;
            
            if (fProcess.Start())
            {
                _isFfmpegRunning = true;
                _processCounter++;
                Console.WriteLine($"Process counter: {_processCounter}");
            }
            
            fProcess.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            fProcess.BeginErrorReadLine();
            return true;
    }



    // this is run in Program.cs
    public bool StartMediaMtx()
    {
        Process mediaProcess = new Process();
        mediaProcess.StartInfo.FileName = "/Users/michaeldefilippi/RiderProjects/VideoRecorder/VideoRecorder/Services/mediamtx";
        mediaProcess.StartInfo.WorkingDirectory = "/Users/michaeldefilippi/RiderProjects/VideoRecorder/VideoRecorder/Services";
        mediaProcess.StartInfo.RedirectStandardError = true;
        mediaProcess.StartInfo.UseShellExecute = false;
        mediaProcess.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
        _isMtxRunning = true;
        _processCounter++;
        mediaProcess.BeginErrorReadLine();
        mediaProcess.Start();
        
        return true;
    }
    // I created this to keep track of processes
    // Its kind of silly...will prob remove
    public async Task ProcessChecker()
    {
        
        while (_processCounter >= 1)
        {
            
            Console.WriteLine($"Processes: {_processCounter} " );
            Console.WriteLine($"Current process: {Process.GetCurrentProcess().ProcessName}");
        }
    }
    
        
    // open a connection to a camera > i am using internal webcam for testing
    public void OpenCvAnalyze(string filename)
    {
        //create video capture object; 0 means default device(webcam)
        using var capture = new VideoCapture(filename, VideoCaptureAPIs.FFMPEG);
        capture.Set(VideoCaptureProperties.FrameWidth, 640);
        capture.Set(VideoCaptureProperties.FrameHeight, 480);
        
        if (!capture.IsOpened())
        {
            Console.WriteLine("Cam not opened");
            return;
        }
        
        // camera is at 30 fps divided by 1000ms = 33 ms between frames
        var sleepTime = (int)Math.Round(1000 / capture.Fps);
        using var window = new Window("capture");
        
        var image = new Mat(); // matrix of pixels

        while (true)
        {
            capture.Read(image);
            if (image.Empty())
            {
                Console.WriteLine("No Video. Break.");
                break; 
            }

            Console.WriteLine("Receiving Video...");
            window.ShowImage(image);
            
            var key = Cv2.WaitKey(1);
            
            if (key == 'q')
            {
                window.Close();
                break;
            }
        }
        capture.Release();
        Cv2.DestroyAllWindows();
    }
    
    
}