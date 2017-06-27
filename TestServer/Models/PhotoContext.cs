using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Web;
using TestServer.Controllers;

namespace TestServer.Models
{
    public class PhotoContext : DbContext
    {
        public DbSet<Person> People { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<Picture> Pictures { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //Database.SetInitializer(new DropCreateDatabaseAlways<PhotoContext>());
            //Database.SetInitializer(new MyDbGenerator<PhotoContext>());
            //modelBuilder.Entity<Photo>().HasOptional(x => x.Face).WithOptionalDependent().WillCascadeOnDelete(false);
            //modelBuilder.Entity<Photo>().HasOptional(x => x.RecFace).WithOptionalDependent().WillCascadeOnDelete(false);
            //modelBuilder.Entity<Person>().HasOptional(x => x.Faces).WithOptionalDependent().WillCascadeOnDelete(false);
            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges()
        {
            var context = new PhotoContext();
            var changedEntities = ChangeTracker.Entries();
            foreach (var changedEntity in changedEntities)
            {
                if (!(changedEntity.Entity is Photo)) continue;
                var entity = (Photo) changedEntity.Entity;
                if (entity.Id == 1) continue;
                if (!entity.IsTheBest) continue;
                switch (changedEntity.State)
                {
                    case EntityState.Added:
                    {
                        var p = context.Photos.First(x => x.Id == 1);
                        PutImage(p.Face.Image, entity.Face.Image,  context);
                    }
                    break;
                    case EntityState.Modified:
                    {
                        var p = context.Photos.First(x => x.Id == 1);
                        var photoPosition =
                            context.People.Where(x => x.Id == entity.OwnerId).Select(x => x.CommonPhotoNumber).First();
                        ReplaceImage(p.Face.Image, entity.Face.Image, photoPosition, context);
                    }
                        break;
                    case EntityState.Deleted:
                    {
                        
                        var p = context.Photos.First(x => x.Id == 1);
                        var newPhoto = context.Photos.FirstOrDefault(x => (x.OwnerId == entity.OwnerId) && x.Id != entity.Id);
                        var position = context.People.First(x => x.Id == entity.OwnerId).CommonPhotoNumber;
                        if (newPhoto == null)
                        {
                            DeleteImage(p.Face.Image, entity.OwnerId, context);
                        }
                        else
                        {                             
                            ReplaceImage(p.Face.Image, newPhoto.Face.Image, position, context);
                        }
                    }   
                        break;
                }
                //TODO:придумать логику обновления общей фотографии
            }
            return base.SaveChanges();
        }

        private void CorrectPhotoNumber(int i, PhotoContext context)
        {
            foreach (var guy in context.People.Where(x => x.Id > i))
            {
                guy.CommonPhotoNumber -= 1;
            }
            context.SaveChanges();
        }

        private void PutImage(byte[] commonPhoto, byte[] newPhoto, PhotoContext context)
        {
            if (commonPhoto != null)
            {
                MemoryStream stream1 = new MemoryStream(commonPhoto);
                Bitmap bmp1 = new Bitmap(stream1);

                MemoryStream stream2 = new MemoryStream(newPhoto);
                Bitmap bmp2 = new Bitmap(stream2);

                int outputImageWidth = bmp1.Width + bmp2.Width;

                int outputImageHeight = bmp1.Height > bmp2.Height ? bmp1.Height : bmp2.Height;

                Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                using (Graphics graphics = Graphics.FromImage(outputImage))
                {
                    graphics.DrawImage(bmp1, new Rectangle(new Point(), bmp1.Size),
                        new Rectangle(new Point(), bmp1.Size), GraphicsUnit.Pixel);
                    graphics.DrawImage(bmp2, new Rectangle(new Point(bmp1.Width, 0), bmp2.Size),
                        new Rectangle(new Point(), bmp2.Size), GraphicsUnit.Pixel);
                }
                MemoryStream m = new MemoryStream();
                var p = context.Photos.First(x => x.Id == 1);
                outputImage.Save(m, ImageFormat.Jpeg);
                p.Face.Image = m.ToArray();
                context.SaveChanges();
                outputImage.Save(@"C:\Users\vinnik\Desktop\Новая папка\img.jpg", ImageFormat.Jpeg);
            }
            else
            {
                MemoryStream stream2 = new MemoryStream(newPhoto);
                var p = context.Photos.First(x => x.Id == 1);
                p.Face.Image = stream2.ToArray();
                context.SaveChanges();
            }
        }

        private void DeleteImage(byte[] commonPhoto, int ownerId, PhotoContext context)
        {
            MemoryStream stream1 = new MemoryStream(commonPhoto);
            Bitmap bmp1 = new Bitmap(stream1);
            int outputImageWidth = bmp1.Width - 100;
            int outputImageHeight = bmp1.Height;

            if (outputImageWidth == 0)
            {
                var photo = context.Photos.First(x => x.Id == 1);
                photo.Face.Image = null;
                context.SaveChanges();
                return;
            }

            Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight,
                PixelFormat.Format32bppArgb);
            int position = context.People.First(x => x.Id == ownerId).CommonPhotoNumber;
            using (Graphics graphics = Graphics.FromImage(outputImage))
            {
                graphics.DrawImage(bmp1, new Rectangle(0, 0, 100 * position, outputImageHeight),
                    new Rectangle(0, 0, 100 * position, outputImageHeight), GraphicsUnit.Pixel);
                graphics.DrawImage(bmp1, new Rectangle(100 * position, 0, outputImageWidth - 100 * position, outputImageHeight),
                    new Rectangle(100 * (position + 1), 0, bmp1.Width - 100 * (position + 1), 100), GraphicsUnit.Pixel);
            }

            outputImage.Save(@"C:\Users\vinnik\Desktop\Новая папка\img.jpg", ImageFormat.Jpeg);
            var p = context.Photos.First(x => x.Id == 1);
            MemoryStream m = new MemoryStream();
            outputImage.Save(m, ImageFormat.Jpeg);
            p.Face.Image = m.ToArray();
            context.SaveChanges();
            CorrectPhotoNumber(position, context);
        }

        private void ReplaceImage(byte[] commonPhoto, byte[] newPhoto, int position, PhotoContext context)
        {
            MemoryStream stream1 = new MemoryStream(commonPhoto);
            Bitmap bmp1 = new Bitmap(stream1);
            if (bmp1.Width/100 <= position)
            {
                PutImage(commonPhoto, newPhoto, context);
                return;
            }

            MemoryStream stream2 = new MemoryStream(newPhoto);
            Bitmap bmp2 = new Bitmap(stream2);

            int outputImageWidth = bmp1.Width;
            int outputImageHeight = bmp1.Height;

            Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight,
                PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(outputImage))
            {
                graphics.DrawImage(bmp1, 0, 0,
                    new Rectangle(0, 0, 100 * position, 100), GraphicsUnit.Pixel);
                outputImage.Save(@"C:\Users\vinnik\Desktop\Новая папка\img.jpg", ImageFormat.Jpeg);
                graphics.DrawImage(bmp2, 100 * position, 0,
                    new Rectangle(0, 0, bmp2.Width, 100), GraphicsUnit.Pixel);
                outputImage.Save(@"C:\Users\vinnik\Desktop\Новая папка\img.jpg", ImageFormat.Jpeg);
                graphics.DrawImage(bmp1, 100 * (position + 1), 0,
                    new Rectangle(100 * (position + 1), 0, bmp1.Width - (100 * (position + 1)), 100), GraphicsUnit.Pixel);
                outputImage.Save(@"C:\Users\vinnik\Desktop\Новая папка\img.jpg", ImageFormat.Jpeg);
            }
            MemoryStream m = new MemoryStream();
            var p = context.Photos.First(x => x.Id == 1);
            outputImage.Save(m, ImageFormat.Jpeg);
            p.Face.Image = m.ToArray();
            context.SaveChanges();

            outputImage.Save(@"C:\Users\vinnik\Desktop\Новая папка\img.jpg", ImageFormat.Jpeg);
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

    public class DbInitializer : DropCreateDatabaseIfModelChanges<PhotoContext>
    {
        protected override void Seed(PhotoContext context)
        {
            Person p = new Person() { Id = 1, ShortName = "Unknown", CommonPhotoNumber = -1 };
            var collectivePicture = new Picture() { Id = 1 };
            Photo collectivePhoto = new Photo() { Owner = p, OwnerId = 1, Id = 1, FaceId = 1, Face = collectivePicture };
            context.People.Add(p);
            context.Photos.Add(collectivePhoto);
            context.Pictures.Add(collectivePicture);

            base.Seed(context);
        }
    }

    public class DropAndCreateDbInitializer : DropCreateDatabaseAlways<PhotoContext>
    {
        protected override void Seed(PhotoContext context)
        {
            Person p = new Person() { Id = 1, ShortName = "Unknown", CommonPhotoNumber = -1 };
            var collectivePicture = new Picture() { Id = 1 };
            Photo collectivePhoto = new Photo() { Owner = p, OwnerId = 1, Id = 1, FaceId = 1, Face = collectivePicture };
            context.People.Add(p);
            context.Photos.Add(collectivePhoto);
            context.Pictures.Add(collectivePicture);

            base.Seed(context);
        }

    }
}