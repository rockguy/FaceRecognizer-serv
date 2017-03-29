using System;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace TestServer.Models
{
    public class Picture
    {
        [Key]
        public int Id { get; set; }
        public byte[] Image { get; set; }
    }
}