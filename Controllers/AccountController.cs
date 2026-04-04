using System.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace VideoRecorder.Controllers;



public class AccountController : Controller
{
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
        if (username == "admin" && password == "admin")
        {
            //claims are pieces of info about the user
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Email, "michaeljdefilippi@yahoo.com"),
                new Claim("null", "null")
            }; 
            ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme); 
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(principal);
            
            return RedirectToAction("Index", "Camera");
        }
        return View();
    }

    //delete the cookie from the browser
    // if no longer auth, redirect to log in screen
    
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}