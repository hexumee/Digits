using Microsoft.EntityFrameworkCore;

namespace Digits {
    class DatabaseContext : DbContext {
        public DbSet<DatabaseElement> RegisteredServices {
            get;
            set;
        }

        public DatabaseContext() {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            optionsBuilder.UseSqlite("Filename=Services.db");
        }

    }
}
