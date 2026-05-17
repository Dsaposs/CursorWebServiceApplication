using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Extensions;

public static class NoteMappingExtensions
{
    public static NoteResponse ToResponse(this Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
    };
}
