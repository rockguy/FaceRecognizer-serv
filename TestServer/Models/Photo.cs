using System.ComponentModel.DataAnnotations;

namespace TestServer.Models
{
    public class Photo
    {
        [Key]
        public int Id { get; set; }
        public bool IsTheBest { get; set; }
        public int FaceId { get; set; }
        public int OwnerId { get; set; }


        public virtual Picture Face { get; set; }
        public virtual Person Owner { get; set; }
        //public virtual Person Owner { get; set; }
    }
}