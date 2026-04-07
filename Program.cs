// See https://aka.ms/new-console-template for more information

using Microsoft.AspNetCore.Authentication.Cookies;
using VideoRecorder.Camera;
using Microsoft.EntityFrameworkCore;
using VideoRecorder.Database;
using VideoRecorder.Services;


//DONE: lower buffering > over 10sec
//TODO: when deleting camera, confirm y or n?
//DONE?: need some kind of loading state, i think liveview is popping up before the stream and crashes
//TODO: Fix ping - shows success incorrectly
//TODO:  create 4 way view
//TODO: fix or delete Stream.ProcessChecker()
//DONE: create camera guid per stream 
//TODO: make sure no 2 of the same ffmpeg process running (implement camera guid)
//TODO: discovered devices: remove duplicates
//TODO: discovered devices: clear devices button (maybe add these to a list<>())
//TODO: discovered devices: show mac address
//TODO: discovered devices: highlight devices that are actually network cams
//TODO: 




// the first thing we want to do is to begin MediaMtx
StreamVideo stream = new StreamVideo();
stream.StartMediaMtx();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(); // tell the app we want controllers and MVC

//register the db so controllers can use it
builder.Services.AddDbContext<VideoRecorderContext>(options =>
   options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")) ); 






builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme) // set up cookie auth
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

    });

var app = builder.Build(); //build it


app.UseStaticFiles(); //allow for css, images, js


app.UseRouting(); //turn on routing so URLS work

// default URL pattern: website.com/Camera/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Camera}/{action=Index}/{id?}");

app.UseAuthentication();
app.UseAuthorization();

app.Run();    //run