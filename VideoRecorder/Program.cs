// See https://aka.ms/new-console-template for more information
// Born: Feb 23 19:10:51 2026

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VideoRecorder.Database;
using VideoRecorder.Models;
using VideoRecorder.Services;


    //DONE: Convert discovered onvif into URI object + save to db
    //DONE: lower buffering > over 10sec
    //DONE: when deleting camera, confirm y or n?
    //DONE?: need some kind of loading state, i think live view is popping up before the stream and crashes
    //DONE: fix or delete Stream.ProcessChecker()
    //DONE: create camera guid per stream 
    //DONE: make sure no 2 of the same ffmpeg process running (implement camera guid)
    //DONE: discovered devices: remove duplicates
    //DONE: have some sort or reconnect logic if retries exceed an amount of time
    //DONE:  create 4 way view
    //DONE: Utilize IDisposable to clean up streams
    //DONE: Bug when adding rtsp camera
    //DONE ping on dashboard does not work for rtsp added devices
    //DONE: check out bugs with onvif discovery
    //DONE: Fix ping - shows success incorrectly
    //DONE: fix response issue for recording button
    //TODO: discovered devices: clear devices button (maybe add these to a list<>())
    //TODO: discovered devices: show mac address
    //TODO: discovered devices: highlight devices that are actually network cams
    //TODO: Convert comments to <param> style
    //TODO: When discovering onvif cams, save button saves all instead of one at a time
    //TODO: program crashes when saving camera with an empty field
    //TODO: create a default admin username built - in
    //TODO: find way to obscure username/pass in rtsp url
    //TODO: find way to obscure username/pass in ffmpeg stderr
    //TODO: add devices through csv
    


    // the first thing we want to do is to begin MediaMtx
    StreamVideo stream = new StreamVideo();
    stream.StartMediaMtx();

    var builder = WebApplication.CreateBuilder(args);

    // tell the app we want controllers and MVC
    builder.Services.AddControllersWithViews(); 

    //register the db so controllers can use it
    builder.Services.AddDbContext<VideoRecorderContext>(options =>
       options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")) ); 

    //add cookie auth stuff
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme) // set up cookie auth
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

        });

    // create one instance of this class per HTTP request and then destroy it
    builder.Services.AddScoped<AltOnvifDiscovery>();
    
    // create one instance of the recording service
    builder.Services.AddSingleton<RecordingService>();
    
    // create an instance of IPasswordHasher; create during the HTTP request and then toss it
    // request the IPasswordHasher interface, initiate the PasswordHasher method
    builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

    //register the health check service
    builder.Services.AddHostedService<CameraHealthService>();

    var app = builder.Build(); //build it


    app.UseStaticFiles(); //allow for CSS, images, js


    app.UseRouting(); //turn on routing so URLS work

    // default URL pattern: website.com/Camera/Index
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.UseAuthentication();
    app.UseAuthorization();

    app.Run();    //run