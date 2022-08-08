﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using static ExplorerEx.Utils.IconHelper;
using File = System.IO.File;

namespace ExplorerEx.Model;

public abstract class FileSystemItem : FileListViewItem {
	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateModified {
		get => dateModified;
		protected set {
			if (dateModified != value) {
				dateModified = value;
				UpdateUI();
			}
		}
	}

	private DateTime dateModified;

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateCreated {
		get => dateCreated;
		protected set {
			if (dateCreated != value) {
				dateCreated = value;
				UpdateUI();
			}
		}
	}

	private DateTime dateCreated;

	public override string GetRenameName() {
		return Name;
	}

	protected override bool InternalRename(string newName) {
		if (newName == null) {
			return false;
		}
		var basePath = Path.GetDirectoryName(FullPath);
		if (Path.GetExtension(FullPath) != Path.GetExtension(newName)) {
			if (!MessageBoxHelper.AskWithDefault("RenameExtension", "#AreYouSureToChangeExtension".L())) {
				return false;
			}
		}
		try {
			FileUtils.FileOperation(FileOpType.Rename, FullPath, Path.Combine(basePath!, newName));
			return true;
		} catch (Exception e) {
			Logger.Exception(e);
		}
		return false;
	}

	/// <summary>
	/// 重新加载图标和详细信息
	/// </summary>
	public void Refresh(LoadDetailsOptions options) {
		if (!IsFolder) {
			LoadIcon(options);
		}
		UpdateUI(nameof(Icon));
		LoadAttributes(options);
	}

	protected FileSystemItem(string fullPath, string name) : base(fullPath, name) { }
}

public class FileItem : FileSystemItem {
	public FileInfo? FileInfo { get; }

	/// <summary>
	/// 是否是可执行文件
	/// </summary>
	public bool IsExecutable => FileUtils.IsExecutable(FullPath);

	/// <summary>
	/// 是否为文本文件
	/// </summary>
	public bool IsEditable => FullPath[^4..] is ".txt" or ".log" or ".ini" or ".inf" or ".cmd" or ".bat" or ".ps1";

	public bool IsZip => FullPath[^4..] == ".zip";

	/// <summary>
	/// 是否为.lnk文件
	/// </summary>
	public bool IsLink => FullPath[^4..] == ".lnk";

	public override string DisplayText => Name;

	protected FileItem() : base(null!, null!) { }

	public FileItem(FileInfo fileInfo) : base(fileInfo.FullName, fileInfo.Name) {
		FileInfo = fileInfo;
		IsFolder = false;
		FileSize = -1;
		Icon = UnknownFileDrawingImage;
	}

	public override void LoadAttributes(LoadDetailsOptions options) {
		if (FileInfo == null) {
			return;
		}
		var type = FileUtils.GetFileTypeDescription(Path.GetExtension(Name));
		if (string.IsNullOrEmpty(type)) {
			Type = "UnknownType".L();
		} else {
			Type = type;
		}
		FileSize = FileInfo.Length;
		DateModified = FileInfo.LastWriteTime;
		DateCreated = FileInfo.CreationTime;
	}

	public override void LoadIcon(LoadDetailsOptions options) {
		if (options.UseLargeIcon) {
			Icon = GetPathThumbnail(FullPath);
		} else {
			Icon = GetSmallIcon(FullPath, false);
		}
	}
}

public class FolderItem : FileSystemItem {
	/// <summary>
	/// 注册的路径解析器
	/// </summary>
	public static readonly HashSet<Func<string, (FolderItem?, PathType)>> PathParsers = new();

	/// <summary>
	/// 将已经加载过的Folder判断完是否为空缓存下来
	/// </summary>
	private static readonly LimitedDictionary<string, bool> IsEmptyFolderDictionary = new(10243);

	/// <summary>
	/// 将所给的path解析为FolderItem对象，如果出错抛出异常，如果没有出错，但格式不支持，返回null
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static (FolderItem?, PathType) ParsePath(string path) {
		// TODO: Shell位置解析
		if (path == "ThisPC".L() || path.ToUpper() is "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" or "::{5E5F29CE-E0A8-49D3-AF32-7A7BDC173478}") {  // 加载“此电脑”
			return (HomeFolderItem.Singleton, PathType.Home);
		}
		path = Environment.ExpandEnvironmentVariables(path.Replace('/', '\\'));
		if (path.Length >= 3) {
			if (path[0] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') && path[1] == ':') {  // 以驱动器作为开头，表示是本地的目录
				if (path.Length == 2 || (path.Length == 3 && path[2] == '\\')) {  // 长度为2或3，表示本地驱动器根目录 TODO: 可能为映射的网络驱动器
					if (Directory.Exists(path)) {
						return (new DiskDriveItem(new DriveInfo(path[..1])), PathType.LocalFolder);
					}
					throw new IOException("#PathNotExistOrAccessDenied".L());
				}
				// 本地的一个文件地址
				var zipIndex = path.IndexOf(@".zip\", StringComparison.CurrentCulture);
				if (zipIndex == -1) { // 没找到.zip\，不是zip文件
					if (Directory.Exists(path)) {
						return (new FolderItem(path.TrimEnd('\\')), PathType.LocalFolder);
					}
					if (File.Exists(path)) {
						return (null, PathType.LocalFile);
					}
					throw new IOException("#PathNotExistOrAccessDenied".L());
				}
				if (path[^1] != '\\') {
					throw new IOException("#ZipMustEndsWithSlash".L());
				}
				return (new ZipFolderItem(path, path[..(zipIndex + 4)], path[(zipIndex + 5)..]), PathType.Zip);
			}
		}
		return PathParsers.Select(pathParser => pathParser.Invoke(path)).FirstOrDefault(result => result.Item1 != null, (null, PathType.Unknown));
	}


	private bool isEmptyFolder;

	protected FolderItem() : base(null!, null!) { }

	public FolderItem(string fullPath): base(fullPath, Path.GetFileName(fullPath)) {
		IsFolder = true;
		FileSize = -1;
		if (IsEmptyFolderDictionary.TryGetValue(fullPath, out var isEmpty)) {
			isEmptyFolder = isEmpty;
			Icon = isEmpty ? EmptyFolderDrawingImage : FolderDrawingImage;
		} else {
			Icon = FolderDrawingImage;
		}
	}

	public override string DisplayText => Name;

	public override void LoadAttributes(LoadDetailsOptions options) {
		isEmptyFolder = FolderUtils.IsEmptyFolder(FullPath);
		Type = isEmptyFolder ? "EmptyFolder".L() : "Folder".L();
		IsEmptyFolderDictionary.Add(FullPath, isEmptyFolder);
		var directoryInfo = new DirectoryInfo(FullPath);
		DateModified = directoryInfo.LastWriteTime;
		DateCreated = directoryInfo.CreationTime;
	}

	public override void LoadIcon(LoadDetailsOptions options) {
		if (isEmptyFolder) {
			Icon = EmptyFolderDrawingImage;
		} else {
			Icon = FolderDrawingImage;
		}
	}

	/// <summary>
	/// 枚举当前目录下的文件项
	/// </summary>
	/// <param name="selectedPath">筛选选中的项</param>
	/// <param name="selectedItem"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	public virtual List<FileListViewItem>? EnumerateItems(string? selectedPath, out FileListViewItem? selectedItem, CancellationToken token) {
		selectedItem = null;
		var list = new List<FileListViewItem>();
		foreach (var directoryPath in Directory.EnumerateDirectories(FullPath)) {
			if (token.IsCancellationRequested) {
				return null;
			}
			var item = new FolderItem(directoryPath);
			list.Add(item);
			if (directoryPath == selectedPath) {
				item.IsSelected = true;
				selectedItem = item;
			}
		}
		foreach (var filePath in Directory.EnumerateFiles(FullPath)) {
			if (token.IsCancellationRequested) {
				return null;
			}
			var item = new FileItem(new FileInfo(filePath));
			list.Add(item);
			if (filePath == selectedPath) {
				item.IsSelected = true;
				selectedItem = item;
			}
		}
		return list;
	}
}

/// <summary>
/// 表示Shell中的特殊文件夹，他有自己的CSIDL并且可以获取IdList
/// </summary>
public interface ISpecialFolder {
	CSIDL Csidl { get; }

	IntPtr IdList { get; }
}
