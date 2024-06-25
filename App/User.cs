using NpgsqlTypes;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1;

public class User
{
    public User()
    {
    }

    public User(int id, string firstName, string lastName, string email, string gender, string ipAddress, bool isActive, DateTime birthdate, int score, Guid uniqueId, DateTime created, decimal rank, List<string> tags)
    {
        Id = id;
        FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
        LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Gender = gender ?? throw new ArgumentNullException(nameof(gender));
        IpAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        IpAddressInet = new NpgsqlInet(ipAddress);
        IsActive = isActive;
        Birthdate = birthdate;
        Score = score;
        UniqueId = uniqueId;
        Created = created;
        Rank = rank;
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }

    [Column(Order = 1)]
    public int Id { get; set; }
    [Column(Order = 2)]
    public string FirstName { get; set; }
    [Column(Order = 3)]
    public string LastName { get; set; }
    [Column(Order = 4)]
    public string Email { get; set; }
    [Column(Order = 5)]
    public string Gender { get; set; }
    [Column(Order = 6)]
    public DateTime Birthdate { get; set; }
    [Column(Order = 7)]
    public bool IsActive { get; set; }
    [Column(Order = 8)]
    public string IpAddress { get; set; }
    
    [Column(Order = 9)]
    public NpgsqlInet IpAddressInet { get; set; }

    [Column(Order = 10)]
    public int Score { get; set; }
    [Column(Order = 11)]
    public decimal Rank { get; set; }
    [Column(Order = 12)]
    public DateTime Created { get; set; }
    [Column(Order = 13)]
    public Guid UniqueId { get; set; }
    [Column(Order = 14)]
    public List<string> Tags { get; set; } = new List<string>();
}

public class UserExtended : User  {
    public UserExtended()
    {
    }

    public UserExtended(User obj): base(obj.Id, obj.FirstName, obj.LastName, obj.Email, obj.Gender, obj.IpAddress, obj.IsActive, obj.Birthdate, obj.Score, obj.UniqueId, obj.Created, obj.Rank, obj.Tags)
    {
        UserJsonData = obj ?? throw new ArgumentNullException(nameof(obj));
    }
    [Column(TypeName = "jsonb", Order = 14)]
    public User UserJsonData { get; set; }
}
