using System;
using System.Linq;
using System.Xml;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vcc.Nolvus.Core.Enums;
using Vcc.Nolvus.Core.Events;
using Vcc.Nolvus.Core.Services;
using Vcc.Nolvus.Core.Interfaces;
using Vcc.Nolvus.Package.Files;
using Vcc.Nolvus.NexusApi;


namespace Vcc.Nolvus.Package.Mods
{
    public abstract class InstallableElement : IInstallableElement
    {
        ModProgress _p = null;        

        #region Properties

        public string Name { get; set; }
        public string Version { get; set; } = string.Empty;
        public string ImagePath { get; set; }
        public string Author { get; set; }
        public ElementAction Action { get; set; }
        public List<ModFile> Files = new List<ModFile>();
        public abstract string ArchiveFolder { get; }
        public int Index { get; set; }
        private System.Drawing.Image FormatImage()
        {
            float Opacity = 0.30F;
            ILibService Lib = ServiceSingleton.Lib;

            System.Drawing.Image Result;
            
            try
            {                
                if (ImagePath != string.Empty)
                {
                    Result = Lib.GetImageFromWebStream(ImagePath);
                }
                else
                {
                    Result = Properties.Resources.mod_def_22;
                }

                Result = Lib.SetImageOpacity(Lib.SetImageGradient(Lib.ResizeKeepAspectRatio(Result, 100, 30)), Opacity);

                return Result;
                
            }
            catch
            {                
                return Lib.SetImageOpacity(Lib.SetImageGradient(Lib.ResizeKeepAspectRatio(Properties.Resources.mod_def_22, 100, 30)), Opacity);
            }
            
        }

        public ModProgress Progress
        {
            get
            {
                if (_p == null)
                {
                    _p = new ModProgress() {
                        Name = Name + " by " + Author + " (v " + Version + ")",
                        Status = "Initializing",
                        Mbs = 0,
                        Image = FormatImage(),
                        Index = Index
                    };                   
                }

                return _p;
            }
        }

        #endregion
        
        public string ExtractSubDir
        {
            get
            {
                return Files.First().CRC32;
            }
        }

        public InstallableElement()
        {                                                     
        }

        #region Methods        

        public virtual void Load(XmlNode Node, List<InstallableElement> Elements)
        {
            Name = Node["Name"].InnerText;            
            Version = Node["Version"].InnerText;
            ImagePath = Node["ImagePath"].InnerText;
            Author = Node["Author"].InnerText;
            Action = ElementAction.Add;
            Index = Elements.Count;

            foreach (XmlNode FileNode in Node.ChildNodes.Cast<XmlNode>().Where(x => x.Name == "Files").FirstOrDefault().ChildNodes.Cast<XmlNode>().ToList())
            {
                ModFile File = Activator.CreateInstance(Type.GetType("Vcc.Nolvus.Package.Files." + FileNode["Type"].InnerText)) as ModFile;

                File.Load(FileNode, this);
                                                
                Files.Add(File);
            }
        }

        public override string ToString()
        {
            return Name;
        }

        protected void HashingProgress(string FileName, int PercentDone)
        {
            Notify(new ModInstallProgressEvent(new HashProgress() {FileName = FileName, ProgressPercentage = PercentDone }));
        }

        protected void DownloadingProgress(object sender, DownloadProgress e)
        {
            Notify(new ModInstallProgressEvent(e));
        }

        protected void ExtractingProgress(object sender, ExtractProgress e)
        {
            Notify(new ModInstallProgressEvent(e));
        }

        protected void CopyingProgress(int Value, int Max)
        {
            Notify(new ModInstallProgressEvent(new CopyProgress() { ItemNumber = Value, MaxItem = Max }));
        }

        protected void UnpackingProgress(string FileName, int Value, int Max)
        {
            Notify(new ModInstallProgressEvent(new UnPackProgress() {FileName = FileName, ItemNumber = Value, MaxItem = Max }));
        }

        protected void PatchingProgress(string FileName, int Value, int Max)
        {
            Notify(new ModInstallProgressEvent(new PatchingProgress() { FileName = FileName, ItemNumber = Value, MaxItem = Max }));
        }

        protected void ArchivingProgress(string FileName, int Value)
        {
            Notify(new ModInstallProgressEvent(new ArchvingProgress() { FileName = FileName, ProgressPercentage = Value }));
        }

        private void Notify(ModInstallProgressEvent Event)
        {            
            Progress.Action = Event.Status.ToString();

            Progress.Mbs = 0;            

            switch (Event.Status)
            {                
                case ModInstallStatus.Downloading:                    
                    if (Event.DownloadInfo.TotalBytesToReceive != 0)
                    {
                        Progress.Mbs = Event.DownloadInfo.Speed;                        
                        Progress.Status = string.Format("Downloading {0} ({1} MB)", Event.DownloadInfo.FileName, Event.DownloadInfo.TotalBytesToReceiveAsString);
                    }
                    else
                    {                        
                        Progress.Status = string.Format(Name + " : Downloading {0})", Event.DownloadInfo.FileName);
                    }

                    Progress.PercentDone = Event.DownloadInfo.PercentDone;
                    break;
                case ModInstallStatus.Hashing:                    
                    
                    Progress.Status = string.Format("Hashing {0}", Event.HashInfo.FileName);
                    Progress.PercentDone = Event.HashInfo.PercentDone;
                    break;
                case ModInstallStatus.Extracting:                    
                    Progress.Status = string.Format("Extracting {0}", Event.ExtractInfo.FileName);
                    Progress.PercentDone = Event.ExtractInfo.PercentDone;                    
                    break;
                case ModInstallStatus.Installing:                    
                    Progress.Status = "Installing files...";
                    Progress.PercentDone = Event.CopyInfo.PercentDone;
                    break;
                case ModInstallStatus.UnPacking:                    
                    Progress.Status = "Unpacking files...";
                    Progress.PercentDone = Event.UnPackInfo.PercentDone;
                    break;
                case ModInstallStatus.Patching:
                    Progress.Status = "Patching files...";
                    Progress.PercentDone = Event.PatchingInfo.PercentDone;
                    break;
                case ModInstallStatus.Archiving:                    
                    Progress.Status = string.Format("Archiving {0}", Event.ArchivingInfo.FileName);
                    Progress.PercentDone = Event.ArchivingInfo.PercentDone;
                    break;

            }            
        }

        public abstract bool IsInstallable();        
        protected virtual async Task DoDownload(Func<IBrowserInstance> Browser)
        {
            var Tsk = Task.Run(async () =>
            {
                try
                {
                    foreach (var File in Files)
                    {                        
                        await File.Download(DownloadingProgress, HashingProgress, ServiceSingleton.Settings.RetryCount, Browser);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            });

            await Tsk;
        }
        protected virtual async Task DoExtract()
        {
            var Tsk = Task.Run(async () =>
            {
                try
                {
                    ServiceSingleton.Files.RemoveDirectory(Path.Combine(ServiceSingleton.Folders.ExtractDirectory, ExtractSubDir), true);

                    foreach (var File in Files)
                    {
                        await File.Extract(ExtractingProgress);
                    }
                }
                catch(Exception ex)
                {
                    ServiceSingleton.Files.RemoveDirectory(Path.Combine(ServiceSingleton.Folders.ExtractDirectory, ExtractSubDir), true);
                    throw ex;
                }
            });

            await Tsk;
        }
        protected abstract Task DoUnpack();
        protected abstract Task DoCopy();
        protected abstract Task DoPatch();          
        protected virtual async Task DoArchive()
        {
            var Tsk = Task.Run(async () => 
            {
                try
                {
                    if (ServiceSingleton.Instances.WorkingInstance.Settings.EnableArchiving)
                    {
                        foreach (var File in Files)
                        {
                            await File.Archive(ArchiveFolder, ArchivingProgress);
                        }
                    }
                    else
                    {
                        foreach (var File in Files)
                        {
                            if (!File.TakenFromArchive)
                            {
                                File.Delete();
                            }                            
                        }
                    }
                }
                catch(Exception ex)
                {
                    throw ex;
                }

            });

            await Tsk;
        }

        public virtual async Task RequestManualNexusDownloadLink(Func<IBrowserInstance> Browser)
        {
            if (Action != ElementAction.Remove)
            {
                await Task.WhenAll(Files.Where(x => x is NexusModFile).Select(async File => 
                {                    
                    if (!File.Exist() || !await File.CRCCheck())
                    {
                        ServiceSingleton.Logger.Log(string.Format("Awaiting manual user download for file {0}", File.FileName));
                        File.DownloadLink = await NexusHelper.MakePostRequest(File.DownloadLink, (File as NexusModFile).FileId);
                    }
                }));
            }
        }   
            
        public virtual async Task Install(CancellationToken Token, ModInstallSettings Settings = null)
        {            
            var Tsk = Task.Run(async () =>
            {
                try
                {
                    Token.ThrowIfCancellationRequested();                    

                    if (Settings != null && Settings.Browser == null) throw new Exception("Browser is not set!");
                    
                    switch (Action)
                    {
                        case ElementAction.Add:
                        case ElementAction.Update:                            
                            await DoDownload(Settings.Browser);
                            Progress.GlobalDone = 15;                     
                            await DoExtract();
                            Progress.GlobalDone = 30;
                            await DoUnpack();
                            Progress.GlobalDone = 45;
                            await DoCopy();
                            Progress.GlobalDone = 60;
                            await DoPatch();
                            Progress.GlobalDone = 75;
                            await DoArchive();
                            Progress.GlobalDone = 100;
                            break;
                        case ElementAction.Remove:
                            await Remove();
                            break;
                    }                    
                }
                catch(Exception ex)
                {                    
                    Progress.GlobalDone = 0;
                    Progress.PercentDone = 0;
                    Progress.Mbs = 0;
                    Progress.Action = string.Empty;
                    ServiceSingleton.Logger.Log("Error during mod installation(" + Name + ") with message : " + ex.Message + Environment.NewLine + "Stack => " + ex.StackTrace);                    
                    throw ex;
                }
            });

            await Tsk;
        }

        public async Task<ModProgress> PrepareProgress()
        {
            return await Task.Run(() => 
            {                
                return Progress;              
            });
        }

        #endregion                                                                                          
      
        public abstract Task Remove();        
    }
}
