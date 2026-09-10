using System.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VideoRecorder.Database;
using VideoRecorder.Models;

namespace VideoRecorder.Controllers;
public class AccountController : Controller

{
    
    private readonly ILogger<UserController> _logger;
    private readonly IPasswordHasher<User> _hasher;
    private readonly VideoRecorderContext _context;

    public AccountController(VideoRecorderContext context, ILogger<UserController> logger, IPasswordHasher<User> hasher)
    {
        _context = context;
        _logger = logger;
        _hasher = hasher;
    }
    
    public IActionResult Login()
    {
        return View();
    }
    
/*******************************************************************************************
 * Claims are basically properties of a user "object"
 * ClaimsIdentity is a collection of key:pair values for a user + wrap it in cookie auth
 * ClaimsPrincipal represents the user as an object
 *******************************************************************************************/
    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        //search the db for a row whose username matches what they typed.
        var user = await _context.User.FirstOrDefaultAsync(u => u.Username == username);
        
        // if no username found or the typed password doesn't match the stored hash
        // we verify the user > the stored hash > the typed password
        if (user == null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, password)
            != PasswordVerificationResult.Success)
        {
            ModelState.AddModelError("", "Invalid username or password");
            TempData["Error"] = "Invalid username or password";
            return View();
        }
        
        //claims are pieces of info about the user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            
        }; 
            
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme); 
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(principal);
            
        return RedirectToAction("Index", "Home");
        
        return View();
    }

    // delete the cookie from the browser
    // if no longer auth, redirect to log in screen
    
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}