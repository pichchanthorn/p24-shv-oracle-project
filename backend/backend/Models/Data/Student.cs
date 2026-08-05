using System.ComponentModel.DataAnnotations;

namespace backend.Models.Data
{
    public class Student
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(255)]
        public string? Email { get; set; }
    }
}