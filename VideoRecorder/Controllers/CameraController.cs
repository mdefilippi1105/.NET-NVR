using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Onvif.Core.Discovery.Models;
using VideoRecorder.Services;
using VideoRecorder.Database;
using VideoRecorder.Network;
using VideoRecorder.Util;
using VideoRecorder.Models;

using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace VideoRecorder.Controllers;

/***************************************************************************
 * IAction is the interface that represents whatever controller action sends
 * back to the browser. It is an interface
 * class inherits everything from .NET controller class
 ***************************************************************************/

[Authorize]
public class CameraController : Controller 
{
    // the tools this controller needs. we store these as fields so 
    // every action method can reach them. 
    private readonly VideoRecorderContext _context;
    private readonly AltOnvifDiscovery _discovery;
    private readonly ILogger<CameraController> _logger;
    private readonly RecordingService _recording;


    // the 3 shared services that all requests talk to
    // these are all registered at Program.CS
    public CameraController(VideoRecorderContext context, 
                            AltOnvifDiscovery discovery, 
                            ILogger<CameraController> logger,
                            RecordingService recording)
    {
        _context = context;
        _discovery = discovery;
        _logger = logger;
        _recording = recording;
    }
    
    /***************************************************************************
    * this is the method that runs when someone visits /camera
    * async means it can wait for db without freezing app
    ***************************************************************************/
    public async Task<IActionResult> Index( int? groupId, string sortBy = "Name" ) // defaults go last, because C# fills arguments left to right
    {
        // fetch all cams and store to cameras.
        List<Camera> cameras = await _context.Camera.ToListAsync();
        
        // self-explanatory sortby logic
        if (sortBy == "Description")
            cameras = cameras.OrderBy(c => c.Description).ToList();
        
        else if (sortBy == "IsEnabled")
            cameras = cameras.Where(c => c.IsEnabled).ToList();
        
        else
            cameras = cameras.OrderBy(c => c.Name).ToList();
        
        // to apply a default sortBy. this allows the sort form to show 
        // the placeholder properly.
        ViewData["sortBy"] = sortBy;
        
        //toss our database items in the viewbag
        ViewBag.Cameras = new SelectList(_context.Camera, "Id", "Name");
        ViewBag.Groups = new SelectList(_context.CameraGroup, "Id", "Name");
        
        return View(cameras); // send list of cams to index to be displayed
    }

    
    /*************************************************************************
     * This runs when someone clicks "+ add camera", just shows empty form
     * The second method is same as Create() but accepts a cam.
     * When form is submitted .NET automatically fills the camera object with
     * whatever the user typed in.
     * Create() is for adding RTSP links
     * AddCamera() is full manual adding
     **************************************************************************/
    
    public IActionResult Create()
    {
        return View(); //show the cshtml
    }
    
    
    [HttpPost]
    public async Task<IActionResult> Create(Camera camera)
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
    
    [HttpGet]
    public async Task<IActionResult> AddCamera()
    {
        await LoadServersAsync(); // have to refill the ViewBag - it doesnt survive the POST
        return View(); //show the cshtml
    }

    [HttpPost]
    public async Task<IActionResult> AddCamera(Camera camera)
    {
        if (ManufacturerTable.DefaultRtspPaths == null)
            camera.Path = "/Streaming/Channels/101";
        
        if (ManufacturerTable.DefaultRtspPaths.ContainsKey(camera.Manufacturer!))
        {
            camera.Path = ManufacturerTable.DefaultRtspPaths[camera.Manufacturer!];
        }
        
        
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

    private async Task LoadServersAsync() =>
        ViewBag.Servers = new SelectList(
            await _context.Server
                .Where(s => s.IsEnabled)
                .OrderBy(s => s.Name)
                .ToListAsync(),
            "Id",
            "Name");
    
    
    // delete the camera
    public async Task<IActionResult> RemoveCamera(Guid id)
    {
        try
        {
            var camera = await _context.Camera.FindAsync(id);
            if (camera == null)
            {
                TempData["Error"] = "Nothing to delete!";
                return NotFound();
            }
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
    public async Task<IActionResult> EditRtsp(Camera camera)
    {
        if (ModelState.IsValid)
        {
            _context.Camera.Update(camera);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Camera edited successfully!";
        }
        return RedirectToAction(nameof(Index));
    }
    
    // edit cameras that were added via RTSP link
    public async Task<IActionResult> EditRtsp(Guid id)
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
    public async Task<IActionResult> EditRtspCamera(Camera camera)
    {
        if (ModelState.IsValid)
        {
            _context.Camera.Update(camera);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Camera updated successfully!";
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
        var connectionTimer = Stopwatch.StartNew();
        
        if (camera.IsEnabled)
        {
            
            stream.StreamDataTest(camera.RtspUrl, camera.Id);
            SharedData.ActiveStreams[camera.Name] = streamId;
            SharedData.StreamCount++;
            
            var data = SharedData.ListStreams();
            Console.WriteLine(data);
            connectionTimer.Stop();
        }
        // set a timer for 9 seconds...10 seems excessive
        else if (connectionTimer.ElapsedMilliseconds > 9000)
        {
            TempData["ConnectFail"] = $"Could not reach {camera.Host}. " + "Please check network connection or " + "try the built in ping tool.";
        }
        
        else
        {
            TempData["Error"] = "Camera is not enabled.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(LiveView), new { id = id });
    }
    
    /***********************************************************************
     * This is basically the same as above
     * Instead of streaming the rtsp URL as one giant string,
     * we put together a camera URL from the camera db objects
     ************************************************************************/
    
    public async Task < IActionResult> OpenCameraObjectSession(Guid id)
    {
        var camera = _context.Camera.Find(id);
        var stream = new StreamVideo();
        var streamId = $"Stream_{camera.Id}";

        // guard - disabled
        if (!camera.IsEnabled)
        {
            TempData["Error"] = "Camera is not enabled.";
            return RedirectToAction(nameof(Index));
        }
        
        // if the dictionary key is not found, roll over to "/live.sdp"
        string path = ManufacturerTable.DefaultRtspPaths.GetValueOrDefault(
            camera.Manufacturer!.ToLower(), "/live.sdp");
        
        //////////////////////////////////
        // all clear - connect to camera
        //////////////////////////////////
        var url = $"rtsp://{camera.Username}:{camera.Password}@{camera.Host}{path}";

        stream.StreamDataTest(url, camera.Id); //connect to the camera
        
        // add to list of streams
        SharedData.ActiveStreams[camera.Name!] = streamId;
        SharedData.StreamCount++;
        Console.WriteLine(SharedData.ListStreams());
        
        
        return RedirectToAction(nameof(LiveView), new { id = id });
    }
    
    
    /***********************************************************************
    * Shut down a camera stream.
     ************************************************************************/

    [HttpPost]
    //take a key (dictionary key - the ip or cam name)
    public IActionResult DestroyStream(string id)
    {
        Console.WriteLine($"id sent: {id}");
        Console.WriteLine($"actual keys: {string.Join(", ", SharedData.StreamObjects.Keys)}");
        
        //thread safe removes key from keypair StreamObjects dict 
        if (SharedData.StreamObjects.TryRemove(id, out var stream))
        {
            stream.Dispose();
            
            SharedData.ActiveStreams.TryRemove(id, out _);
        }
        return Ok();
    }
    
    /***********************************************************************
     * when you log in ping all cams
     ************************************************************************/

    public async Task<IActionResult> StreamStatuses()
    {
        var statuses = await _context.Camera
            .AsNoTracking()
            .Select(c => new
            {
                id = c.Id, 
                ready = c.IsOnline,
                armed = c.UserToggledRecording,
                recording = c.IsRecording
            })
            .ToListAsync();

        return Json(statuses);
    }
        
    /*****************************************************************
     * Here are the LiveView actions
     * Moving forward, the plan is to have single
     * view, 2x2, 3x3, 4x4 etc.
     * We start by querying the camera table. 
     * Grab set amount of rows with Take(int)
     * Run the query and return a List<Camera>
     *****************************************************************/

    public IActionResult LiveView(Guid id)
    {
        var camera = _context.Camera.Find(id);
        if (camera == null) return NotFound();
        
        ViewData["CameraId"] = id;
        return View(camera);
    }

    public IActionResult LiveView2X2()
    {
        //for each camera c, sort by its CreatedAt
        var cameras = _context.Camera.OrderBy(c => c.CreatedAt).Take(4).ToList();

        foreach (var cam in cameras)
        {
            if (!cam.IsEnabled) continue;
            if (SharedData.ActiveStreams.ContainsKey(cam.Name)) continue;
            
            var url = $"rtsp://{cam.Username}:{cam.Password}@{cam.Host}{cam.Path}";
            var stream = new StreamVideo();
            stream.StreamDataTest(url, cam.Id);
        }

        ViewBag.Cameras = cameras;
        return View();
    }

    public IActionResult LiveView2X3()
    {
        //for each camera c, sort by its CreatedAt
        var cameras = _context.Camera.OrderBy(c => c.CreatedAt).Take(6).ToList();

        foreach (var cam in cameras)
        {
            if (!cam.IsEnabled) continue;
            if (SharedData.ActiveStreams.ContainsKey(cam.Name)) continue;
            
            var url = $"rtsp://{cam.Username}:{cam.Password}@{cam.Host}{cam.Path}";
            var stream = new StreamVideo();
            stream.StreamDataTest(url, cam.Id);
        }

        ViewBag.Cameras = cameras;
        return View();
        
    }

    public IActionResult LiveView2X4()
    {
        var cameras = _context.Camera.OrderBy(c => c.CreatedAt).Take(8).ToList();
        foreach (var cam in cameras)
        {
            if (!cam.IsEnabled) continue;
            if (SharedData.ActiveStreams.ContainsKey(cam.Name)) continue;

            var url = $"rtsp://{cam.Username}:{cam.Password}@{cam.Host}{cam.Path}";
            var stream = new StreamVideo();
            stream.StreamDataTest(url, cam.Id);
        }
        
        ViewBag.Cameras = cameras;
        return View();
    }
    
    
    /***********************************************************************
    * Onvif library discovery method:
    * 
    ************************************************************************/

    public async Task<IActionResult> Discover(string username, string password)
    {
        try
        {
            var discovery = new AltOnvifDiscovery();
            await discovery.DiscoverAsync(username, password);
            await SaveDiscoveredCameras(discovery.OnvifUriList!);
            return Json(discovery.OnvifUriList);
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Json(e.Message);
            return Json(new List<DiscoveryDevice>());
        }
    }
    
    [HttpPost]
    public async Task SaveDiscoveredCameras(List<string> rtspUrls)
    {
        foreach (var rtspUrl in rtspUrls)
        {
            var uri = new Uri(rtspUrl);
            var userInfo = uri.UserInfo.Split(':');
            
            var camera = new Camera
            {
                IsOnvif =  true,
                Name = uri.Host,
                RtspUrl = rtspUrl,
                Scheme = uri.Scheme,
                Host = uri.Host,
                Port = uri.Port,
                Path = uri.AbsolutePath,

                // if the array has 1 element, take the first one: username
                // if the array has at least 2 elements, take the second one: password
                // the reason for this is if there is no user creds in the string
                // so splitting on ":" with empty string will get IndexOutOfBounds
                Username = userInfo.Length > 0 ? userInfo[0] : null,
                Password = userInfo.Length > 1 ? userInfo[1] : null,

                IsEnabled = true,
                CreatedAt = DateTime.Now,
            };
            
            _context.Camera.Add(camera);
        }
        await _context.SaveChangesAsync();
    }
    
    
    /************************************************************************
     *  Ping device and ping subnet
     ************************************************************************/

    public  IActionResult PingAddress(string ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            TempData["Error"] = "Cannot be null. Please provide a valid IP address.";
            return RedirectToAction(nameof(Create));
        }
        
        var ping = new DevicePingTools();
        try
        {
           if (ping.RunPing(ip))
            TempData["Success"] = "Ping successful to address " + ip;
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            TempData["Error"] = "Ping failed.";
        } 
        
        return RedirectToAction(nameof(Index));
    } 
    
    /************************************************************************
     *  Udp Discovery - will likely be removed. I have this bound to a
     * button in index, so it can always be repurposed. 
     ************************************************************************/

    public IActionResult UdpDiscover()
    {
        var results = new List<string>();

        try
        {
            UdpDiscoveryTools.SendDiscovery();
            results = UdpDiscoveryTools.ReceiveResponse();
            Thread.Sleep(3000);
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
    
    /************************************************************************
     * Scanning subnet class. Converted from old Java Ping scanner :)
     * We need [FromBody] tells ASP.NET  to look in the body for
     * this value otherwise it's going to come in as null.
     ************************************************************************/

    [HttpPost]
    public async Task <IActionResult> DetectDevicesOnSubnet([FromBody] ScanRequest request)
    {
      var scanner = new DevicePingTools();
      var results =   await scanner.ScanSubnet(request.Ip);
      return Json(results);
    }

    public class ScanRequest
    {
        public string Ip { get; set; }
    }
    
    
    
    /************************************************************************
     * Group Assignment Methods
     * The basic flow is: pick a camera, pick a group, submit
     * 2 Assign() functions. one for get, one for post. post is labeled
     ************************************************************************/

    public IActionResult Assign()
    {
        // we only use SelectList for populating <select> from a database table
        // arg 2 = value field, arg 3 = display text
        ViewBag.Cameras = new SelectList(_context.Camera.ToList(), "Id", "Name");
        ViewBag.Groups = new SelectList(_context.CameraGroup.ToList(), "Id", "Name");
        return View();
    }

    [HttpPost]
    public IActionResult Assign(Guid cameraId, int groupId)
    {
        var cam = _context.Camera.Find(cameraId);
        cam.CameraGroupId = groupId;
        
        _context.SaveChanges();
        return RedirectToAction("Index");
    }

    public IActionResult AddCameraGroup()
    {
        ViewBag.Cameras = new SelectList(_context.Camera.ToList(), "Id", "Name");
        ViewBag.Groups = new SelectList(_context.CameraGroup.ToList(), "Id", "Name");
       
        // this is to add the cameras to an unordered list
        ViewBag.CameraList = _context.Camera.ToList();
        
        return View();
    }

    [HttpPost]
    public IActionResult AddCameraGroup(string name, List<Guid> cameraIds)
    {
        //make a new group. when we hit SaveChanges() it will generate and ID for us
        var group = new CameraGroup { Name = name };
        if (string.IsNullOrEmpty(name))
        {
            TempData["Error"] = "Cannot be null. Please provide a valid name.";
            return RedirectToAction("Index");  
        }
            
        
        _context.CameraGroup.Add(group);
        _context.SaveChanges();

        foreach (var id in cameraIds)
        {
            var cam = _context.Camera.Find(id);
            cam!.CameraGroupId = group.Id;
        }
        _context.SaveChanges();
        return RedirectToAction("Index");
    }
    
    
    // this page renders the group assignments.
    public IActionResult ViewGroupAssignments(int? groupId)
    {
        ViewBag.AllGroups = _context.CameraGroup.ToList();
        
        // we use Include() here so each group has its own cameras. 
        // no model needed. view reads from the ViewBag
        var groups = _context.CameraGroup.Include(g => g.Cameras).AsQueryable();

        if (groupId.HasValue)
            groups = groups.Where(g => g.Id == groupId);

        ViewBag.Groups = groups.ToList();
        
        return View();
    }
    
    
}    

