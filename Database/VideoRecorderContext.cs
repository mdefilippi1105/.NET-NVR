using Microsoft.EntityFrameworkCore;


namespace VideoRecorder.Database;
/************************************************************************
// collection that maps directly to database table
// core base class that manages db interaction
// this is the bridge between the code and SQL Server
************************************************************************/
public class VideoRecorderContext : DbContext 
{

    public VideoRecorderContext(DbContextOptions<VideoRecorderContext> options)
        : base(options)
    {
    }

    public DbSet<Camera.Camera> Camera { get; set; }
    

}

