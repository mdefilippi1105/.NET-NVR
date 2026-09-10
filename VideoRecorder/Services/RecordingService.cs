using System.Collections.Concurrent;
using System.Diagnostics;
using VideoRecorder.Database;
using VideoRecorder.Models;

namespace VideoRecorder.Services;

public class RecordingService
{
    //  the id is the key, the running Process is the value
    // concurrent with multiple requests hitting it 
    private readonly ConcurrentDictionary<Guid, Process> _recordings = new();
    
    private readonly string _ffmpegPath =
        "/Users/michaeldefilippi/RiderProjects/VideoRecorder/VideoRecorder/ThirdParty/ffmpeg";
    

    /*************************************************
     * First, grab the camera from the db
     * Based on the server OS, it will write to the selected drive path
     *
     * ffmpeg args explained:
     * 1. TCP instead of UDP, copy codec its already using (H264)
     * 2. segment output - series of files vs 1 big file
     * new segment every 300 seconds
     * 3. set each timestamp to start at 0 instead of continuing from the stream
     * 4. pass mp4 options into each segment. write a fragmented mp4
     * so files stay playable even if the service dies mid-segment.
     * 5. {dir}/date and time format
     ***************************************************/
    
    public void Start(Camera camera, string cameraUrl)
    {
        
        // check for stale processes before starting a fresh one
        if (_recordings.TryGetValue(camera.Id, out var existingProcess))
        {
            if (!existingProcess.HasExited)
                return;
            
            _recordings.TryRemove(camera.Id, out _);
            existingProcess.Dispose();
        }
        
        if (String.IsNullOrWhiteSpace(camera.Server?.DrivePath))
            throw new InvalidOperationException($"Camera {camera.Id} has no server drive path.");

        
        // $@ keeps the slashes verbatim and string interpolation
        var recordingDirectory = Path.Combine(camera.Server.DrivePath, camera.Id.ToString());
        Directory.CreateDirectory(recordingDirectory);

        var segmentPattern = Path.Combine(recordingDirectory, "%Y%m%d_%H%M%S.mp4");

        var recordProcess = new Process();
        recordProcess.StartInfo.FileName = _ffmpegPath;
        recordProcess.StartInfo.Arguments = $"-rtsp_transport tcp -i \"{cameraUrl}\" " +
                                            $"-c copy " +
                                            $"-f segment -segment_time 300 " +
                                            $"-reset_timestamps 1 -strftime 1 " +
                                            $"-segment_format_options movflags=+frag_keyframe+empty_moov " +
                                            $"\"{segmentPattern}\"";
        
        // c# commands are now the keyboard instead of terminal keyboard
        recordProcess.StartInfo.RedirectStandardInput = true;
        // direct method, no shell needed
        recordProcess.StartInfo.UseShellExecute = false;
        // suppresses a blank cmd window from popping up - for Windows OS only
        recordProcess.StartInfo.CreateNoWindow = true;
        
        try
        {
            recordProcess.Start();
        }
        catch
        {
            _recordings.TryRemove(camera.Id, out _);
            recordProcess.Dispose();
            throw;
        }

        
        if (!_recordings.TryAdd(camera.Id, recordProcess))
        {
            recordProcess.Dispose();
            return;
        }

    }

    // we pull the reference out of the dictionary, to kill
    // the process and then dispose of it cleanly
    // if cameraId comes back false, out var process is null
    public void Stop(Guid cameraId)
    {
        if (!_recordings.TryRemove(cameraId, out var process))
            return;
        try
        {
            if (!process.HasExited)
            {
                //simulate hitting q - shuts down the process clean
                process.StandardInput.Write("q");
                //flush out the buffer
                process.StandardInput.Flush();

                if (!process.WaitForExit(1000))
                    process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            process.Dispose();
        }
        
    }
    
    // allow recording if the user enabled recording button, the cam is online,
    // and the camera is enabled.
    public void RecordingAuthorize(Camera camera)
    {
        // look up the camera id
        // if no entry, never started, false
        // if entry dead - ffmpeg quit, false
        // if entry alive - ffmpeg running, true
        _recordings.TryGetValue(camera.Id, out var process);
        
        var processIsAlive = process is not null && !process.HasExited;
        
        camera.IsRecording = processIsAlive;
        
        var recordingAllowed = camera.UserToggledRecording && camera.IsOnline && camera.IsEnabled;
        
        // if no server found - set IsRecording to false
        if (camera.Server is null || string.IsNullOrWhiteSpace(camera.Server.DrivePath))
        {
            camera.IsRecording = false;
            return;
        }
        
        //cam online, enabled and toggle button clicked, but NOT currently recording
        if (recordingAllowed && !camera.IsRecording)
        {
            var url = $"rtsp://{camera!.Username}:{camera.Password}@{camera.Host}{camera.Path}";
            Start(camera, url);
            
        }
        
        // if any 3 of the "allowed" parameters are not satisfied
        else if (!recordingAllowed && camera.IsRecording)
        {
            Stop(camera.Id); // "shut it down" -jon taffer
            camera.IsRecording = false;
        }
    }
    
    //old method, keeping for reference
    // public void Start(Guid cameraId, string cameraUrl)
    // {
    //     
    //     // guard - if cam in dictionary, return
    //     if (_recordings.ContainsKey(cameraId))
    //         return;
    //     
    //     // $@ keeps the slashes verbatim and string interpolation
    //     var recordingDirectory = $@"/Users/michaeldefilippi/rec-test/{cameraId}";
    //     Directory.CreateDirectory(recordingDirectory);
    //     
    //
    //     var recordProcess = new Process();
    //     recordProcess.StartInfo.FileName = "/Users/michaeldefilippi/RiderProjects/VideoRecorder/VideoRecorder/ThirdParty/ffmpeg";
    //     recordProcess.StartInfo.Arguments = $"-rtsp_transport tcp -i \"{cameraUrl}\" " +
    //                                         $"-c copy " +
    //                                         $"-f segment -segment_time 300 " +
    //                                         $"-reset_timestamps 1 -strftime 1 " +
    //                                         $"-segment_format_options movflags=+frag_keyframe+empty_moov " +
    //                                         $"\"{recordingDirectory}/%Y%m%d_%H%M%S.mp4\"";
    //     
    //     // c# commands are now the keyboard instead of terminal keyboard
    //     recordProcess.StartInfo.RedirectStandardInput = true;
    //     
    //     
    //     // direct method, no shell needed
    //     recordProcess.StartInfo.UseShellExecute = false;
    //     
    //     // suppresses a blank cmd window from popping up - for Windows OS only
    //     recordProcess.StartInfo.CreateNoWindow = true;
    //     
    //     // start the recordings
    //     recordProcess.Start();
    //     
    //     // lastly we add the id and the process to the dictionary
    //     _recordings.TryAdd(cameraId, recordProcess);
    // }
    
    
    
    
    
    
    
    
    
    
    
}