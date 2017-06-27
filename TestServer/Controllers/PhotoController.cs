using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Web;
using System.Web.Mvc;
using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using TestServer.Models;
using TestServer.Support;


namespace TestServer.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.Disabled)]
    public class PhotoController : Controller
    {

        readonly PhotoContext PhotoContext = new PhotoContext();
        readonly LBPHFaceRecognizer FaceRecognizer = new LBPHFaceRecognizer();
        string DataPath = "";
        CascadeClassifier FaceClassifier;
        readonly int FaceLenght = 100;

        [HttpGet]
        public ActionResult ClearDb()
        {
            PhotoContext.Database.Delete();
            Database.SetInitializer(new DropAndCreateDbInitializer());
            PhotoContext.Database.Create();
            PhotoContext.Database.CreateIfNotExists();
            PhotoContext.Database.Initialize(true);

            return Json("Ok", JsonRequestBehavior.AllowGet);
        }
        [HttpPost]

        public JsonResult AddPhoto(string shortName, HttpPostedFileBase photo)
        {
            try
            {
                var b = new Bitmap(Image.FromStream(photo.InputStream));
                var img = new Image<Rgb, byte>(b);

                var gray = img.Convert<Gray, Byte>();
                FaceClassifier =
                    new CascadeClassifier(Server.MapPath("~/App_Data/") + "haarcascade_frontalface_default.xml");
                var faces = FaceClassifier.DetectMultiScale(gray, 1.1, 2, Size.Empty, new Size(b.Width, b.Height));
                b = CropImage(b, faces.First());

                b = new Bitmap(b, new Size(FaceLenght, FaceLenght));

                var s = new MemoryStream();
                b.Save(s, ImageFormat.Bmp);
                var p = new Photo() {Face = new Picture() {Image = s.ToArray()}};
                var owner = PhotoContext.People.FirstOrDefault(x => x.ShortName == shortName);
                if (owner == null)
                {
                    int i = PhotoContext.People.Max(x => x.CommonPhotoNumber) + 1;
                    owner = new Person() {ShortName = shortName, CommonPhotoNumber = i};

                    PhotoContext.People.Add(owner);
                    PhotoContext.SaveChanges();
                    p.IsTheBest = true;
                }
                p.OwnerId = owner.Id;
                owner.Faces.Add(p);
                PhotoContext.Photos.Add(p);
                PhotoContext.Pictures.Add(p.Face);
                PhotoContext.SaveChanges();
                if(PhotoContext.Photos.Count(x => true) > 2) Update(p);
                else Train();
                owner = PhotoContext.People.First(x => x.ShortName == shortName);
                var Id = PhotoContext.Photos.Where(x => x.OwnerId == owner.Id).Max(x => x.Id);
                return Json(Id);
            }
            catch (Exception e)
            {
                return Json("Лицо не было найдено");
            }
        }

        [HttpGet]
        public ActionResult ShowImages()
        {
            var p = PhotoContext.Photos.OrderByDescending(x => x.Id).FirstOrDefault();
            if (p == null) return Json("Нет изображений для отображения");
            return File(p.Face.Image, "image/Jpeg");
        }

        [HttpGet]
        public FileResult GetPersonPhoto(int id)
        {
            var s = PhotoContext.Photos.Where(x => x.OwnerId == id).OrderBy(x => x.Id).Select(x => x.Face.Image);
            var ls = s.ToList();
            if (ls.Count == 0)
            {
                var p = PhotoContext.People.FirstOrDefault(x => x.Id == id);
                PhotoContext.People.Remove(p);
                return null;
            }
            var result = ls[0];
            ls.RemoveAt(0);

            result = ls.Aggregate(result, HelpClass.ConcatPhoto);
            return File(result, id.ToString());
        }

        [HttpGet]
        public JsonResult MakeMainPhoto(int id)
        {
            var ownerId = PhotoContext.Photos.Where(x => x.Id == id).Select(x => x.OwnerId).First();
            var oldPhoto = PhotoContext.Photos.First(x => x.OwnerId == ownerId && x.IsTheBest);
            oldPhoto.IsTheBest = false;
            var newPhoto = PhotoContext.Photos.First(x => x.Id == id);
            newPhoto.IsTheBest = true;
            PhotoContext.SaveChanges();
            return Json("Ok", JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult DeletePhoto(int id)
        {
            var p = PhotoContext.Photos.First(x => x.Id == id);
            if (PhotoContext.Photos.Count(x => x.OwnerId == p.OwnerId) == 1)
            {
                PhotoContext.Photos.Remove(p);
                PhotoContext.SaveChanges();
                DeletePerson(p.OwnerId);
            }
            else
            {
                PhotoContext.Photos.Remove(p);
                PhotoContext.SaveChanges();
            }
            return Json("Ok", JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public void AddPerson(Person person)
        {
            PhotoContext.People.Add(person);
            PhotoContext.SaveChanges();
        }

        [HttpPost]
        public JsonResult DeletePerson(Person person)
        {
            var photos = PhotoContext.Photos.Where(x => x.OwnerId == person.Id);
            PhotoContext.Photos.RemoveRange(photos);
            var p = PhotoContext.People.First(x => x.Id == person.Id);
            PhotoContext.People.Remove(p);
            PhotoContext.SaveChanges();
            ClearPictures();
            return Json("ok");
        }
        [HttpGet]
        public void DeletePerson(int id)
        {
            var p = PhotoContext.People.First(x => x.Id==id);
            PhotoContext.People.Remove(p);
            PhotoContext.SaveChanges();
            ClearPictures();
        }

        private void ClearPictures()
        {
            var photos = PhotoContext.Photos.Select(x => x.Id);
            var pictures = PhotoContext.Pictures.Where(x => !photos.Contains(x.Id));
            PhotoContext.Pictures.RemoveRange(pictures);
            PhotoContext.SaveChanges();
        }

        [HttpPost]
        public void AddPersons(Person[] people)
        {
            foreach (var person in people)
            {
                PhotoContext.People.Add(person);
            }
            PhotoContext.SaveChanges();
        }

        [HttpGet]
        public JsonResult GetPersonDetail(int id)
        {
            if (id == 0) return Json("Человек не узнан");
            var s = PhotoContext.People.Select(x => new
            {
                x.Id, x.ShortName, x.City, x.FirstName, x.LastName, x.MiddleName}).FirstOrDefault(x => x.Id == id);
            return Json(s,JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetPersonInfo(string shortName)
        {
            var s = PhotoContext.People.Select(x => new
            {
                x.Id,
                x.ShortName,
                x.City,
                x.FirstName,
                x.LastName,
                x.MiddleName
            }).FirstOrDefault(x => x.ShortName == shortName);
            return Json(s, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetPersonPhotoInfo(int id)
        {
            
            var s = PhotoContext.Photos.Where(x => x.OwnerId == id).OrderBy(x => x.Face.Id).Select(x => new {x.Id, x.OwnerId, x.Owner.ShortName, x.IsTheBest}).ToList();

            return Json(s, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult UpdatePerson(Person person)
        {
            if (person.Id != 0)
            {
                var p = PhotoContext.People.FirstOrDefault(x => x.Id == person.Id);
                if (p == null) return null;
                p.City = person.City;
                p.FirstName = person.FirstName;
                p.LastName = person.LastName;
                p.MiddleName = person.MiddleName;
                p.ShortName = person.ShortName;
                PhotoContext.SaveChanges();
                return Json("ok");
            }
            else
            {
                PhotoContext.People.Add(person);
                return Json("ok");
            }
        }

        [HttpPost]
        public JsonResult ChangeOwner(int id, string shortName)
        {
            var photo = PhotoContext.Photos.FirstOrDefault(x => x.Id == id);
            var owner = PhotoContext.People.FirstOrDefault(x => x.ShortName == shortName);
            if (owner == null)
            {
                int i = PhotoContext.People.Max(x => x.CommonPhotoNumber) + 1;
                owner = new Person() { ShortName = shortName, CommonPhotoNumber = i };

                PhotoContext.People.Add(owner);
                PhotoContext.SaveChanges();
                photo.IsTheBest = true;
            }
            photo.OwnerId = owner.Id;
            PhotoContext.SaveChanges();

            return Json("ok");
        }

        [HttpGet]
        public JsonResult AllPersons()
        {
            return Json(PhotoContext.People.Where(x=> x.Id != 1).OrderBy(x => x.CommonPhotoNumber).Select(x => new {x.Id, x.ShortName, x.FirstName, x.LastName}).ToList(),JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public FileResult CollectivePhoto()
        {
            var p = PhotoContext.Photos.FirstOrDefault(x => x.Id == 1);
            return (p.Face.Image == null)? null : File(p.Face.Image, "CollectivePhoto");
        }

        [HttpPost]
        public void AddPhotos(string[] shortNames, HttpPostedFileBase[] files)
        {
            int i = 0;
            foreach (var shortName in shortNames)
            {
                var b = new Bitmap(Image.FromStream(files[i].InputStream));
                var img = new Image<Rgb, byte>(b);

                var gray = img.Convert<Gray, Byte>();
                FaceClassifier =
                    new CascadeClassifier(Server.MapPath("~/App_Data/") + "haarcascade_frontalface_default.xml");
                var faces = FaceClassifier.DetectMultiScale(gray, 1.2, 10, Size.Empty);
                b = CropImage(b, faces.First());

                b = new Bitmap(b, new Size(FaceLenght, FaceLenght));

                var s = new MemoryStream();
                b.Save(s, ImageFormat.Bmp);
                var p = new Photo() {Face = new Picture() {Image = s.ToArray()}};
                var owner = PhotoContext.People.FirstOrDefault(x => x.ShortName == shortName);
                if (owner == null)
                {
                    owner = new Person() {ShortName = shortName, CommonPhotoNumber = i };
                    PhotoContext.People.Add(owner);
                    PhotoContext.SaveChanges();

                }
                p.OwnerId = owner.Id;
                owner.Faces.Add(p);
                PhotoContext.Photos.Add(p);
                PhotoContext.Pictures.Add(p.Face);
                i++;
            }
            PhotoContext.SaveChanges();
        }

        private Bitmap CropImage(Bitmap source, Rectangle section)
        {
            // An empty bitmap which will hold the cropped image
            Bitmap bmp = new Bitmap(section.Width, section.Height);

            Graphics g = Graphics.FromImage(bmp);

            // Draw the given area (section) of the source image
            // at location 0,0 on the empty bitmap (bmp)
            g.DrawImage(source, 0, 0, section, GraphicsUnit.Pixel);

            return bmp;
        }

        [HttpGet]
        public ActionResult AllPhotos()
        {
            var c = PhotoContext.Photos/*.Where(x => x.Id != 1)*/;
            return c.ToList().Count == 0 ? null : View(c);
        }

        [HttpGet]
        public JsonResult RecognizeByteArray(byte[] photo)
        {
            DataPath = Server.MapPath("~/App_Data/Faces");
            FaceRecognizer.Load(DataPath);
            var depthImage = new Image<Gray, byte>(photo.Rank, (photo.Length/photo.Rank)) {Bytes = photo};
            var s = FaceRecognizer.Predict(depthImage).Label;
            var person = PhotoContext.People.FirstOrDefault(x => x.Id == s);
            return Json(person, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult RecognizeConcatPhoto(HttpPostedFileBase photo)
        {
            
            DataPath = Server.MapPath("~/App_Data/Faces");
            //var uploadedFile = new byte[photo.InputStream.Length];
            //photo.InputStream.Read(uploadedFile, 0, uploadedFile.Length);
            FaceRecognizer.Load(DataPath);

            Bitmap b = new Bitmap(Image.FromStream(photo.InputStream));

            List<int> facesList = new List<int>();
            List<FaceRecognizer.PredictionResult> PredictionResult = new List<FaceRecognizer.PredictionResult>();

            FaceClassifier = new CascadeClassifier(Server.MapPath("~/App_Data/") + "haarcascade_frontalface_default.xml");

            var persons = Enumerable.Empty<object>()
             .Select(r => new { Id = 0, ShortName = "", FirstName = "", LastName = "" }) // prototype of anonymous type
             .ToList();
            persons.Remove(new {Id = 0, ShortName = "", FirstName = "", LastName = ""});

            for (int j = 0; j < b.Width / 100; j++)
            {
                var b1 = b.Clone(new Rectangle(j * 100, 0, 100, 100), PixelFormat.Format32bppArgb);
                
                var gray = new Image<Gray, byte>(b1);
                gray._EqualizeHist();
                Rectangle[] faces;
                try
                {
                    faces = FaceClassifier.DetectMultiScale(gray, 1.1, 1, new Size(50,50), new Size(gray.Width, gray.Height));
                }
                catch (Exception e)
                {
                    return Json("There is no Faces: " + e);
                }
                var predict = FaceRecognizer.Predict(gray);
                if (predict.Distance < 110)
                {
                    var s = PhotoContext.People.Where(x => x.Id == predict.Label).Select(x => new { x.Id, x.ShortName, x.FirstName, x.LastName}).ToList();
                    persons.AddRange(s);
                    PredictionResult.Add(predict);
                    facesList.Add(predict.Label);
                }
                else
                {
                    persons.Add(new { Id = 0, ShortName = "Unknown", FirstName = "", LastName = "" });
                }
                
            }

            FaceRecognizer.Dispose();
            return Json(persons, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult RecognizePhoto(HttpPostedFileBase photo)
        {
            DataPath = Server.MapPath("~/App_Data/Faces");
            //var uploadedFile = new byte[photo.InputStream.Length];
            //photo.InputStream.Read(uploadedFile, 0, uploadedFile.Length);
            FaceRecognizer.Load(DataPath);

            var b = new Bitmap(Image.FromStream(photo.InputStream));
            FaceClassifier = new CascadeClassifier(Server.MapPath("~/App_Data/") + "haarcascade_frontalface_default.xml");

            var gray = new Image<Gray, byte>(b);
            
            Rectangle[] faces;
            try
            {
                faces = FaceClassifier.DetectMultiScale(gray, 1.2, 3, Size.Empty);
            }
            catch (Exception e)
            {
                return Json("There is no Faces: " + e);
            }

            var facesList = new List<int>();
            var PredictionResult = new List<FaceRecognizer.PredictionResult>();
            foreach (var b2 in faces.Select(f => new Bitmap(CropImage(b, f),new Size(FaceLenght, FaceLenght))))
            {
                gray = new Image<Gray, Byte>(b2);
                gray._EqualizeHist();
                var predict = FaceRecognizer.Predict(gray);
                if (predict.Distance < 110)
                {
                    PredictionResult.Add(predict);
                    facesList.Add(predict.Label);
                }
                else
                {
                    facesList.Add(0);
                }
            }
            var s = PhotoContext.People.Where(x => facesList.Contains(x.Id)).Select(x=> new {x.Id, x.FirstName, x.LastName, x.ShortName});
            FaceRecognizer.Dispose();
            return Json(s, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult Recognizephotos(HttpPostedFileBase[] photos)
        {
            DataPath = Server.MapPath("~/App_Data/Faces");
            List<FaceRecognizer.PredictionResult> results = new List<FaceRecognizer.PredictionResult>();
            foreach (var photo in photos)
            {
                var uploadedFile = new byte[photo.InputStream.Length];
                photo.InputStream.Read(uploadedFile, 0, uploadedFile.Length);
                FaceRecognizer.Load(DataPath);

                Bitmap b = new Bitmap(Image.FromStream(photo.InputStream));
                Image<Rgb, byte> img = new Image<Rgb, byte>(b);
                Image<Gray, byte> gray = img.Convert<Gray, Byte>();

                FaceClassifier =
                    new CascadeClassifier(Server.MapPath("~/App_Data/") + "haarcascade_frontalface_default.xml");
                Rectangle[] faces;
                try
                {
                    faces = FaceClassifier.DetectMultiScale(gray, 1.2, 3, Size.Empty);
                }
                catch (Exception e)
                {
                    return Json("There is no Faces: " + e);
                }
                List<int> facesList = new List<int>();
                List<FaceRecognizer.PredictionResult> PredictionResult = new List<FaceRecognizer.PredictionResult>();
                foreach (var f in faces)
                {
                    var b2 = new Bitmap(CropImage(b, f), new Size(FaceLenght, FaceLenght));
                    gray = new Image<Gray, Byte>(b2);

                    var predict = FaceRecognizer.Predict(gray);
                    PredictionResult.Add(predict);
                    facesList.Add(predict.Label);
                }
                var s =
                    PhotoContext.People.Where(x => facesList.Contains(x.Id))
                        .Select(x => new {x.FirstName, x.LastName, x.ShortName});
                results.AddRange(PredictionResult);
            }
            return Json(results, JsonRequestBehavior.AllowGet);
        }

        private void Train()
        {
            var inputImages = new List<Image<Gray, byte>>();
            var labelsList = new List<int>();
            foreach (var photo in PhotoContext.Photos.Where(x => x.Id != 1).ToList())
            {
                MemoryStream str = new MemoryStream(photo.Face.Image);
                Bitmap b = new Bitmap(Image.FromStream(str));
                Image<Gray, byte> img = new Image<Gray, byte>(b);
                img._EqualizeHist();
                inputImages.Add(img);
                labelsList.Add(photo.OwnerId);
            }
            DataPath = Server.MapPath("~/App_Data/Faces");
            FaceRecognizer.Train(inputImages.ToArray(), labelsList.ToArray());
            FaceRecognizer.Save(DataPath);
        }

        private void Update(Photo photo)
        {
            var inputImages = new List<Image<Gray, byte>>();
            var labelsList = new List<int>();
            MemoryStream str = new MemoryStream(photo.Face.Image);
            Bitmap b = new Bitmap(Image.FromStream(str));
            Image<Gray, byte> img = new Image<Gray, byte>(b);
            img._EqualizeHist();
            inputImages.Add(img);
            labelsList.Add(photo.OwnerId);
            DataPath = Server.MapPath("~/App_Data/Faces");
            FaceRecognizer.Update(inputImages.ToArray(), labelsList.ToArray());
            FaceRecognizer.Save(DataPath);
        }

        public static void RefreshListOfBestFaces()
        {
            
        }
    }
}