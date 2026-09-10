using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using VideoRecorder.Database;
using VideoRecorder.Models;

namespace VideoRecorder.Services;

/***************************************************
 * This is the background service to check camera
 * status every few seconds. Without this service,
 * we would only get the camera health status
 * every time we hit refresh or the page loads.
 * When a request comes in, ASP.NET creates a "scope" -
 * a little box for the request. Background service
 * has no request so no box.
 * We also use recorderCount = count 360 times x 10 = once per hour
 * 
 ****************************************************/
public class CameraHealthService : BackgroundService
{
    
    // service is a singleton, 
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecordingService _recording;
    private readonly ILogger<CameraHealthService> _logger;
    private int _recorderCount = 0;
    
    
    public CameraHealthService(IServiceScopeFactory scopeFactory,
        RecordingService recording,
        ILogger<CameraHealthService> logger)
    {
        _scopeFactory = scopeFactory;
        _recording = recording;
        _logger = logger;
    }
    
    
    // this is our logic to delete the recordings
    // camera retention time is set by
    // the adding camera / edit camera pages
    private void DeleteOldRecordings(Camera camera)
    {
        if (string.IsNullOrWhiteSpace(camera.Server?.DrivePath))
            return;
        
        var dir = Path.Combine(camera.Server.DrivePath, camera.Id.ToString());
        if (!Directory.Exists(dir))
            return;
        
        // this is the oldest video file we keep.
        // anything older than this time stamp gets deleted.
        var cutoff = DateTime.Now.AddDays(-camera.RetentionDays);
        try
        {
            foreach (var file in Directory.GetFiles(dir, "*.mp4"))
            {
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    public static async Task<bool> SendAsyncHealthCheck(Camera camera)
    {
        try
        {
            if (camera == null)
                return false;

            // use camera.Host if it's not null, else parse RtspUrl and pull host out of it
            var host = camera.Host ?? new Uri(camera.RtspUrl!).Host; 
            
            if (string.IsNullOrEmpty(host))
                return false;
            
            // set to using to implement IDisposable
            using var ping = new Ping();
            var reply = ping.Send(host, 500);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return  false;
        }
    }

    
    // background service requires this method
    // host calls it once at startup and runs for the rest of apps life
    protected override async Task ExecuteAsync(CancellationToken stopToken)
    {
        while (!stopToken.IsCancellationRequested)
        {
            // dispose it when finished.
            using var scope = _scopeFactory.CreateScope();
            
            // make a new instance, don't remember everything the main Dbcontext loads
            // then make a fresh connection then return it on dispose()
            var context = scope.ServiceProvider.GetRequiredService<VideoRecorderContext>();
            
            try
            {
                var cameras = await context.Camera
                    .Include(c => c.Server)
                    .ToListAsync(stopToken);

                // pass counter. the loop runs every 10 seconds,
                // so this tracks how many loops have passed
                _recorderCount++;

                // check each cam to auth recording
                foreach (var cam in cameras)
                {
                    _recording.RecordingAuthorize(cam);
                    cam.IsOnline = await SendAsyncHealthCheck(cam);
                    _logger.LogInformation("{Name}: rec={R} toggled={T}", cam.Name, cam.IsRecording, cam.UserToggledRecording);
                }

                // 360 counts x 10 seconds = once per hour
                // then we run DeleteRecordings()
                if (_recorderCount % 360 == 0)
                {
                    foreach (var cam in cameras)
                    {
                        DeleteOldRecordings(cam);
                    }
                }

                await context.SaveChangesAsync(stopToken);
            }
            catch (Exception exception)
            {
                _logger.LogError("Health poll failure: " + exception.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stopToken);
            
        }
        
    }
    
}