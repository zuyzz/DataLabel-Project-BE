using System;
using System.Collections.Generic;

namespace DataLabelProject.Business.Models;

public partial class User
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = null!;

    /// <summary>
    /// Hashed password for authentication
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Email { get; set; }

    /// <summary>
    /// VN format: 0xxxxxxxxx or +84xxxxxxxxx
    /// </summary>
    public string? PhoneNumber { get; set; }

    public Guid RoleId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<Annotation> Annotations { get; set; } = new List<Annotation>();

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<Assignment> AssignmentUsers { get; set; } = new List<Assignment>();

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Role UserRole { get; set; } = null!;
}
