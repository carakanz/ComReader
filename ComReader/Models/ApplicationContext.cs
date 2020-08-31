using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComReader.Models
{
    class ApplicationContext : DbContext
    {
        public DbSet<DBFrame> Frames { get; set; }
        public DbSet<DBEvent> Eventes { get; set; }
        public DbSet<DBSeries> Series { get; set; }
        public ApplicationContext(DbContextOptions<ApplicationContext> options)
            :base(options)
        {
            Database.EnsureCreated();
        }
    }
}
