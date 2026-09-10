using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoRecorder.Database;
using VideoRecorder.Migrations;
using VideoRecorder.Models;
using VideoRecorder.Services;

namespace VideoRecorder.Controllers;

public class RecordingController : Controller
{
    // the tools this controller needs. we store these as fields so 
    // every action method can reach them.
    private readonly VideoRecorderContext _context;
    private readonly RecordingService _recording;
    private readonly ILogger<RecordingController> _logger;
    
    
    // the 3 shared services that all requests talk to
    // these are all registered at Program.CS
    public RecordingController(VideoRecorderContext context, 
                               ILogger<RecordingController> logger,
                               RecordingService recording)
    {
        _context = context;
        _recording = recording;
        _logger = logger;
    }

    // grab the camera id and start recording 
    [HttpPost]
    public async Task<IActionResult> StartRecording(Guid cameraId)
    {
        var camera = await _context.Camera
            .Include(c => c.Server)
            .FirstOrDefaultAsync(c => c.Id == cameraId);
        var url = $"rtsp://{camera!.Username}:{camera.Password}@{camera.Host}{camera.Path}";
        
        // guard checks
        if (camera! == null || !camera.IsEnabled || !camera.IsOnline)
            return NotFound();
        
        _logger.LogInformation("Starting recording for {CameraName}", camera.Name);
        
        _recording.Start(camera, url);
        
        return Ok();

    }

    [HttpPost]
    public async Task<IActionResult> StopRecording(Guid cameraId)
    {
        var camera = await _context.Camera.FindAsync(cameraId);
        if (camera == null)
            return NotFound();
        
        _logger.LogInformation("Stopping recording for {CameraName}", camera.Name);
        
        _recording.Stop(camera.Id);
        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> ToggleRecording(Guid id)
    {
        var camera  = await _context.Camera
                            .Include(c => c.Server)
                            .FirstOrDefaultAsync(c => c.Id == id);
        
        if (camera == null)
            return NotFound();
        
        // this makes sure the bool is flipped in memory
        // flip it to the opposite of whatever it is now
        camera.UserToggledRecording = !camera.UserToggledRecording;
        
        _logger.LogInformation("Recording toggled for {CameraName}", camera.Name);
        
        _recording.RecordingAuthorize(camera);
        
        await _context.SaveChangesAsync();
        
        return RedirectToAction("Index", "Camera");
    }



}





























