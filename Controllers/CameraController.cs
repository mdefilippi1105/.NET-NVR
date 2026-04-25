using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Onvif.Core.Discovery.Models;
using VideoRecorder.Camera;
using VideoRecorder.Services;
using VideoRecorder.Database;
using VideoRecorder.Network;
using VideoRecorder.Util;


namespace VideoRecorder.Controllers;

/***************************************************************************
 * IAction is the interface that represents whatever controller action sends
 * back to the browser. Its an interface
 * class inherits everything from .NET controller class
 ***************************************************************************/

[Authorize]
public class CameraController : Controller
{
    //var to hold the database connection
    private readonly VideoRecorderContext _context;

    public CameraController(VideoRecorderContext context)
    {
        _context = context;
    }

    
    /***************************************************************************
    * this is the method that runs when someone visits /camera
    * async means it can wait for db without freezing app
    ***************************************************************************/
    public async Task<IActionResult> Index()
    {
        var cameras = await _context.Camera.ToListAsync(); // fetch all cams and store to cameras.

        return View(cameras); // send list of cams to index to be displayed
    }

    
    /*************************************************************************
     * This runs when someone clicks "+ add camera", just shows empty form
     * The second method is same as Create() but accepts a cam.
     * When form is submitted .NET automatically fills the camera object with
     * whatever the user typed in.
     **************************************************************************/
    
    public IActionResult Create()
    {
        return View(); //show the cshtml
    }
    
    
    [HttpPost]
    public async Task<IActionResult> Create(Camera.Camera camera)
    {
        if (ModelState.IsValid) // check data validation
        {
            _context.Add(camera); // add cam to database context

            await _context.SaveChangesAsync(); // save to the db
            TempData["Success"] = "Camera saved!";
            return RedirectToAction(nameof(Index)); // after saving, send user back to cam list page
        }
        else
        {
            TempData["Error"] = "Could not add camera.";
        }

        return View(camera);
    }
    
    
    // delete the camera
    public async Task<IActionResult> RemoveCamera(Guid id)
    {
        try
        {
            var camera = await _context.Camera.FindAsync(id);
            _context.Remove(camera);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Camera removed from database.";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            TempData["Error"] = "Could not remove camera.";
        }
        return RedirectToAction(nameof(Index));
    }

    
    //edit the camera
    public async Task<IActionResult> EditCamera(Guid id)
    {
        var camera = await _context.Camera.FindAsync(id);
        if (camera == null)
        {
            TempData["Error"] = "Could not find camera.";
            return NotFound();
        }

        return View(camera);
    }

    [HttpPost]
    public async Task<IActionResult> EditCamera(Camera.Camera camera)
    {
        if (ModelState.IsValid)
        {
            _context.Camera.Update(camera);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Camera edited successfully!";
        }
        return RedirectToAction(nameof(Index));
    }

    /***********************************************************************
     * Grab the rtsp url from Camera.RtspUrl table
     * Open up connection via ffmpeg -> push to media mtx
     * Add the camera stream ID to stream dictionary
     * if the stream id is in the dict > throw error ideally
     ************************************************************************/
    
    public IActionResult OpenRtspSession(Guid id)
    {
        var camera = _context.Camera.Find(id);
        var stream = new StreamVideo();
        var streamId = $"Stream_{camera.Id}";
        
        
        //safety check- if the camera stream already exists, throw an error
        if (SharedData.ActiveStreams.ContainsKey(camera.Name))
        {
            TempData["Error"] = "Camera stream already active.";
            return RedirectToAction(nameof(Index));
        }
        
        if (camera.IsEnabled)
        {
            stream.StreamDataTest(camera.RtspUrl, camera.Id);
            SharedData.ActiveStreams[camera.Name] = streamId;
            SharedData.StreamCount++;
            
            var data = SharedData.ListStreams();
            Console.WriteLine(data);
        }
        
        else
        {
            TempData["Error"] = "Camera is not enabled.";
            Console.WriteLine("STREAM ERROR CAM UNENABLED");
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(LiveView), new { id = id });
    }

    //action for live viewing
    public IActionResult LiveView(Guid id)
    {
        ViewData["CameraId"] = id;
        return View();
    }

    
    /***********************************************************************
    * Onvif library discovery method:
    * Honestly I am not sure if its macOS socket issues
    * or if the issue is a bug in the library.
    ************************************************************************/

    public async Task<IActionResult> Discover()
    {
        try
        {
            var discovery = new OnvifDiscovery();
            var devices = await discovery.DiscoverAsync();
            return Json(devices);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Json(new List<DiscoveryDevice>());
        }
    }

    
    // This is a test action
    public IActionResult TestDiscover()
    {
        var fakeDevices = new List<object>
        {
            new { name = "Axis Cam", address = "192.168.0.125" },
            new { name = "Cam", address = "192.168.0.126" },
        };
        return Json(fakeDevices);
    }
    
    /************************************************************************
     *  Ping device and ping subnet
     ************************************************************************/

    public async Task<IActionResult> PingAddress(string ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            TempData["Error"] = "Cannot be null. Please provide a valid IP address.";
            return RedirectToAction(nameof(Create));
        }
        
        var ping = new DevicePingTools();
        try
        {
            ping.RunPing(ip);
            TempData["Success"] = "Ping successful to address " + ip;
            Console.WriteLine("TempData set to success");
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            TempData["Error"] = "Ping failed.";
        } 
        
        return RedirectToAction(nameof(Create));
    } 
    
    /************************************************************************
     *  Udp Discovery
     ************************************************************************/

    public IActionResult UdpDiscover()
    {
        var results = new List<string>();

        try
        {
            UdpDiscoveryTools.SendDiscovery();
            results = UdpDiscoveryTools.ReceiveResponse();
            UdpDiscoveryTools.Stop();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        return Json(results);
    }

    public IActionResult Arp()
    {
        DevicePingTools.AddressResolution();
        return Json(DevicePingTools.AddressResolution());
    }
    
    
}    

