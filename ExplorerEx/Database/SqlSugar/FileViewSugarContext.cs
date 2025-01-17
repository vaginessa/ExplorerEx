﻿using ExplorerEx.Database.Interface;
using ExplorerEx.Model;

namespace ExplorerEx.Database.SqlSugar; 

/// <summary>
/// 这个不需要Cache
/// </summary>
public class FileViewSugarContext : SugarContext<FileView>, IFileViewDbContext {
	public FileViewSugarContext() : base("FileViews.db") { }

	public void Update(FileView item) {
		ConnectionClient.Updateable<FileView>().Where(i => i.FullPath == item.FullPath);
	}
}