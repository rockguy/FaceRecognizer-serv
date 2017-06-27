using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Web;

namespace TestServer.Models
{
    public class Person
    {
        [Key]
        public int Id { get; set; }

        public int CommonPhotoNumber { get; set; }

        [StringLength(50)]
        public string FirstName { get; set; }
        [StringLength(50)]
        public string MiddleName { get; set; }
        [StringLength(50)]
        public string LastName { get; set; }
        [StringLength(30)]
        public string City { get; set; }
        [StringLength(50)]
        [UniqueValue]
        [Required]
        public string ShortName { get; set; }

        public virtual List<Photo> Faces { get; set; } = new List<Photo>();

        public Person()
        {
        }

        public Person(string shortName)
        {
            ShortName = shortName;
        }
    }
}