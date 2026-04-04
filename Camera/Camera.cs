using System.ComponentModel.DataAnnotations;

namespace VideoRecorder.Camera;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    [Required]
    [Display(Name = "RTSP URL")]
    public string RtspUrl { get; set; }
    
    [Display(Name = "ENABLED")]
    public bool IsEnabled { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    
}
