﻿namespace CloudCrate.Application.DTOs.Folder.Response;

public class FolderContentsResponse
{
    public List<FolderOrFileItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public Guid? ParentFolderId { get; set; }
    public Guid? ParentOfCurrentFolderId { get; set; }
    public string FolderName { get; set; } = "Root";
}