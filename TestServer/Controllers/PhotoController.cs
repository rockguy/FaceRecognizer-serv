using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using TestServer.Models;


namespace TestServer.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.Disabled)]
    public class PhotoController : Controller
    {

        readonly PhotoContext PhotoContext = new PhotoContext();
        readonly FaceRecognizer FaceRecognizer = new LBPHFaceRecognizer();
        string DataPath = "";
        CascadeClassifier FaceClassifier;
        readonly int FaceLenght = 100;

        [HttpGet]
        public ActionResult ClearDb()
        {
            foreach (var Photoe in PhotoContext.Photoes)
            {
                PhotoContext.Photoes.Remove(Photoe);
            }
            foreach (var Person in PhotoContext.People)
            {
                PhotoContext.People.Remove(Person);
            }
            foreach (var Picture in PhotoContext.Pictures)
            {
                PhotoContext.Pictures.Remove(Picture);
            }
            PhotoContext.SaveChanges();
            return Json("Ok", JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult AddPhoto(string shortName, HttpPostedFileBase file)
        {        
            var b = new Bitmap(Image.FromStream(file.InputStream));
            var img = new Image<Rgb, byte>(b);

            var gray = img.Convert<Gray, Byte>();
            FaceClassifier = new CascadeClassifier(Server.MapPath("~/App_Data/") + "haarcascade_frontalface_default.xml");
            var faces = FaceClassifier.DetectMultiScale(gray, 1.2, 10, Size.Empty);
            b = CropImage(b, faces.First());
            
            b = new Bitmap(b, new Size(FaceLenght, FaceLenght));

            var s = new MemoryStream();
            b.Save(s, ImageFormat.Bmp);
            var p = new Photo() { Face = new Picture() {Image = s.ToArray()} };
            var owner = PhotoContext.People.FirstOrDefault(x => x.ShortName == shortName);
            if (owner == null)
            {
                owner = new Person() {ShortName = shortName};
                PhotoContext.People.Add(owner);
                PhotoContext.SaveChanges();
            }
            p.OwnerId = owner.Id;
            owner.Faces.Add(p);
            PhotoContext.Photoes.Add(p);
            PhotoContext.Pictures.Add(p.Face);
            PhotoContext.SaveChanges();
            Train();
            return Json("Face was Added");
        }

        [HttpPost]
        public void AddPhotoes(string[] shortNames, HttpPostedFileBase[] files)
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
                    owner = new Person() {ShortName = shortName};
                    PhotoContext.People.Add(owner);
                    PhotoContext.SaveChanges();
                }
                p.OwnerId = owner.Id;
                owner.Faces.Add(p);
                PhotoContext.Photoes.Add(p);
                PhotoContext.Pictures.Add(p.Face);
                i++;
            }
            PhotoContext.SaveChanges();
        }

        [HttpPost]
        public void AddPerson(Person person)
        {
            PhotoContext.People.Add(person);
            PhotoContext.SaveChanges();
        }

        [HttpPost]
        public void DeletePerson(Person person)
        {
            PhotoContext.People.Remove(person);
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
        public ActionResult AllPhotoes()
        {
            var c = PhotoContext.Photoes;
            return View(c);
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
        public JsonResult RecognizePhoto(HttpPostedFileBase photo)
        {
            DataPath = Server.MapPath("~/App_Data/Faces");
            var uploadedFile = new byte[photo.InputStream.Length];
            photo.InputStream.Read(uploadedFile, 0, uploadedFile.Length);
            FaceRecognizer.Load(DataPath);

            Bitmap b = new Bitmap(Image.FromStream(photo.InputStream));
            Image<Rgb, byte> img = new Image<Rgb, byte>(b);
            Image<Gray, byte> gray = img.Convert<Gray, Byte>();

            FaceClassifier = new CascadeClassifier(Server.MapPath("~/App_Data/") + "haarcascade_frontalface_default.xml");
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
                var b2 = new Bitmap(CropImage(b, f),new Size(FaceLenght, FaceLenght));
                gray = new Image<Gray, Byte>(b2);

                var predict = FaceRecognizer.Predict(gray);
                PredictionResult.Add(predict);
                facesList.Add(predict.Label);
            }
            var s = PhotoContext.People.Where(x => facesList.Contains(x.Id)).Select(x=> new {x.FirstName, x.LastName, x.ShortName}).FirstOrDefault();
            
            return Json(s, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult RecognizePhotoes(HttpPostedFileBase[] photoes)
        {
            DataPath = Server.MapPath("~/App_Data/Faces");
            List<FaceRecognizer.PredictionResult> results = new List<FaceRecognizer.PredictionResult>();
            foreach (var photo in photoes)
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

        [HttpGet]
        public ActionResult ShowImages()
        {
            var p = PhotoContext.Photoes.OrderByDescending(x => x.Id).FirstOrDefault();
            if (p == null) return Json("Нет изображений для отображения");
            return File(p.Face.Image, "image/Jpeg");
        }

        private void Train()
        {
            var inputImages = new List<Image<Gray, byte>>();
            var labelsList = new List<int>();
            foreach (var photo in PhotoContext.Photoes.ToList())
            {
                MemoryStream str = new MemoryStream(photo.Face.Image);
                Bitmap b = new Bitmap(Image.FromStream(str));
                Image<Gray, byte> img = new Image<Gray, byte>(b);

                inputImages.Add(img);
                labelsList.Add(photo.OwnerId);
            }
            DataPath = Server.MapPath("~/App_Data/Faces");
            FaceRecognizer.Train(inputImages.ToArray(), labelsList.ToArray());
            FaceRecognizer.Save(DataPath);
        }
    }
}