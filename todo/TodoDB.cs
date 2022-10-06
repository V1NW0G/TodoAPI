using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

class Todo
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string Description { get; set; }

    public DateTime Duedate { get; set; }

    public bool Status { get; set; }



}

class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options) : base(options) { }
    public DbSet<Todo> Todos => Set<Todo>();
}