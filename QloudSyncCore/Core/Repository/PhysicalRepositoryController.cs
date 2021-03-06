using System;
using System.IO;
using System.Collections.Generic;
using GreenQloud.Model;
using System.Linq;
using GreenQloud.Synchrony;
using System.Threading;
using GreenQloud.Persistence.SQLite;

namespace GreenQloud.Repository
{
    public class PhysicalRepositoryController : AbstractController, IPhysicalRepositoryController
    {
        public List<RepositoryItem> GetItems (string prefixDir) {
            DirectoryInfo dir = new DirectoryInfo(prefixDir);
            List<RepositoryItem> list = new List<RepositoryItem>();
            if(dir.Exists) {
                foreach (FileInfo fileInfo in dir.GetFiles ("*", System.IO.SearchOption.TopDirectoryOnly).ToList ()) {
                    string key = fileInfo.FullName.Substring(repo.Path.Length);
                    RepositoryItem localFile = RepositoryItem.CreateInstance (repo, key);
                    list.Add (localFile);
                }
                foreach (DirectoryInfo dirInfo in dir.GetDirectories ("*", System.IO.SearchOption.AllDirectories).ToList ()){
                    string key = dirInfo.FullName.Substring(repo.Path.Length);
                    if (!key.EndsWith (Path.DirectorySeparatorChar.ToString()))
                        key += Path.DirectorySeparatorChar;
                    RepositoryItem localFile = RepositoryItem.CreateInstance (repo, key);
                    list.Add (localFile);
                }
            }
            return list;
        } 

        public List<GreenQloud.Model.RepositoryItem> RootFolders {
            get {
                DirectoryInfo dir = new DirectoryInfo(repo.Path);
                List<RepositoryItem> list = new List<RepositoryItem>();
                if(repo != null) {
                    if(dir.Exists) {
                        foreach (DirectoryInfo dirInfo in dir.GetDirectories ("*", System.IO.SearchOption.TopDirectoryOnly).ToList ()){
                            string key = dirInfo.FullName.Substring(repo.Path.Length);
                            if (!key.EndsWith (Path.DirectorySeparatorChar.ToString()))
                                key += Path.DirectorySeparatorChar;
                            RepositoryItem localFile = RepositoryItem.CreateInstance (repo, key);
                            list.Add (localFile);
                        }
                    }
                }
                return list;
            }
        }












        //OLD
        public PhysicalRepositoryController (LocalRepository repo) : base (repo)
        {

        }

        //TODO understand and refactor
        public bool IsSync(RepositoryItem item){
            //if(item.IsFolder)
            //    return true;
            //if(item.ETag == string.Empty)
            //    throw new Exception ("Remote Hash not exists");
            //item.LocalMD5Hash = CalculateMD5Hash(item);
            //return item.RemoteMD5Hash == item.LocalMD5Hash;
            return true;
        }
        


        public bool Exists (RepositoryItem item)
        {
            return Exists(item.LocalAbsolutePath);
        }
        public bool Exists (string path)
        {
            return File.Exists (path) || Directory.Exists (path);
        }

        public void Delete (RepositoryItem item)
        {
            if(Directory.Exists(item.LocalAbsolutePath)){
                var dir = new DirectoryInfo(item.LocalAbsolutePath);
                DeleteDir (dir);
            }
            if(File.Exists(item.LocalAbsolutePath)){
                DeleteFile(item.LocalAbsolutePath);
            }
        }

        public void Copy (RepositoryItem item)
        {
            string path = item.ResultItem.LocalAbsolutePath;

            if (Exists (item.ResultItem))
                Delete (item.ResultItem);
            if (item.IsFolder)
            {
                if(!Directory.Exists(path)){
                    var dir = new DirectoryInfo(item.LocalAbsolutePath);
                    CopyDir (dir, path);
                }
            }else{
                CopyFile(item.LocalAbsolutePath, path);
            }
        }

        public void Move (RepositoryItem item)
        {
            string path = item.ResultItem.LocalAbsolutePath;

            if (Exists (item.ResultItem))
                Delete (item.ResultItem);
            if (item.IsFolder)
            {
                MoveDir(item.LocalAbsolutePath, path);
            }else{
                MoveFile(item.LocalAbsolutePath, path);
            }
        }

        public RepositoryItem CreateItemInstance (string fullLocalName)
        {
            FileInfo file = new FileInfo (fullLocalName);
            if (file.Exists){
                string key = fullLocalName.Substring (repo.Path.Length);
                return RepositoryItem.CreateInstance(repo, key);
            }
            return null;
        }
        #region Core Management

        public void CreateFolder(RepositoryItem item){
            if (!Exists(item)){
                CreatePath (item.LocalAbsolutePath);
                BlockWatcher (item.LocalAbsolutePath);
                Directory.CreateDirectory (item.LocalAbsolutePath);
                UnblockWatcher (item.LocalAbsolutePath);   
            }
        }
        
        public void CreatePath (string path)
        {
            string parent = path.Substring(0, path.LastIndexOf(Path.DirectorySeparatorChar.ToString()));

            if (parent == string.Empty || parent.EndsWith(Path.VolumeSeparatorChar.ToString()))
                return;

            CreatePath(parent);

            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                path += Path.DirectorySeparatorChar;

            if (!Directory.Exists(path))
            {
                BlockWatcher(path);
                Directory.CreateDirectory(path);
                UnblockWatcher(path);
            }
        }

        public void MoveFile(string path, string toPath)
        {
            BlockWatcher (path);
            BlockWatcher (toPath);
            File.Move(path, toPath);
            UnblockWatcher (path);
            UnblockWatcher (toPath);
        }
        public void MoveDir(string path, string toPath){
            BlockWatcher (path);
            BlockWatcher (toPath);
            Directory.Move(path, toPath);
            UnblockWatcher (path);
            UnblockWatcher (toPath);
        }

        public void CopyFile(string path, string toPath)
        {
            BlockWatcher (toPath);
            File.Copy(path, toPath);
            UnblockWatcher (toPath);
        }
        
        public void DeleteFile(string path)
        {
            BlockWatcher (path);
            File.Delete (path);
            UnblockWatcher (path);
        }

        public void CopyDir(DirectoryInfo dir, string to)
        {
            if (!Directory.Exists(to))
            {
                BlockWatcher(to);
                DirectoryInfo di = Directory.CreateDirectory(to);
                UnblockWatcher(to);
            }
            dir.GetDirectories().ToList().ForEach(directory => CopyDir(directory, to + "/" + directory.Name));

            List<FileInfo> files = dir.GetFiles("*", SearchOption.AllDirectories).ToList();
            foreach (FileInfo file in files)
            {
                CopyFile(file.FullName, to + "/" + file.Name);
            }
        }

        public void DeleteDir(DirectoryInfo dir)
        {
            if (dir != null)
            {
                string dirName = dir.FullName;
                if (!dirName.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    dirName += Path.DirectorySeparatorChar;

                dir.GetDirectories().ToList().ForEach(directory => DeleteDir(directory));

                List<FileInfo> files = dir.GetFiles("*", SearchOption.AllDirectories).ToList();
                foreach (FileInfo file in files)
                {
                    DeleteFile(file.FullName);
                }

                BlockWatcher(dirName);
                Directory.Delete(dirName);
                UnblockWatcher(dirName);
            }
        }
        #endregion
    }
}

