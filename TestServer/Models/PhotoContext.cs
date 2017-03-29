using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Web;

namespace TestServer.Models
{
    public class PhotoContext : DbContext
    {
        public DbSet<Person> People { get; set; }
        public DbSet<Photo> Photoes { get; set; }
        public DbSet<Picture> Pictures { get; set; }

        static PhotoContext()
        {
             Database.SetInitializer(new DropCreateDatabaseIfModelChanges<PhotoContext>());
            //Database.SetInitializer(new DropCreateDatabaseAlways<PhotoContext>());
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //Database.SetInitializer(new DropCreateDatabaseAlways<PhotoContext>());
            Database.SetInitializer(new MyDbGenerator<PhotoContext>());
            //modelBuilder.Entity<Photo>().HasOptional(x => x.Face).WithOptionalDependent().WillCascadeOnDelete(false);
            //modelBuilder.Entity<Photo>().HasOptional(x => x.RecFace).WithOptionalDependent().WillCascadeOnDelete(false);
            //modelBuilder.Entity<Person>().HasOptional(x => x.Faces).WithOptionalDependent().WillCascadeOnDelete(false);
            base.OnModelCreating(modelBuilder);
        }

    }

    public class MyDbGenerator<T> : IDatabaseInitializer<T> where T : DbContext
    {
        public void InitializeDatabase(T context)
        {
            if (!context.Database.CompatibleWithModel(false))
            {
                context.Database.Delete();
                context.Database.Create();
            }
            //Fetch all the father class's public properties 
            var fatherPropertyNames = typeof(DbContext).GetProperties().Select(pi => pi.Name).ToList();
            //Loop each dbset's T 
            foreach (PropertyInfo item in typeof(T).GetProperties().Where(p => fatherPropertyNames.IndexOf(p.Name) < 0).Select(p => p))
            {
                //fetch the type of "T" 
                Type entityModelType = item.PropertyType.GetGenericArguments()[0];
                var allfieldNames = from prop in entityModelType.GetProperties()
                                    where prop.GetCustomAttributes(typeof(UniqueValue), true).Count() > 0
                                    select prop.Name;
                foreach (string s in allfieldNames)
                {
                    context.Database.ExecuteSqlCommand("alter table " + item.Name + " add unique(" + s + ")");
                }
            }
        }
    }
}