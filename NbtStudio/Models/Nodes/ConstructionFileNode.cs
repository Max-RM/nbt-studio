using Aga.Controls.Tree;
using fNbt;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NbtStudio
{
    public class ConstructionFileNode : ModelNode<object>
    {
        public readonly ConstructionFile File;
        public ConstructionFileNode(NbtTreeModel tree, INode parent, ConstructionFile file) : base(tree, parent)
        {
            File = file;
            File.ContentsChanged += File_ContentsChanged;
            File.OnSaved += File_OnSaved;
        }

        protected override void SelfDispose()
        {
            File.ContentsChanged -= File_ContentsChanged;
            File.OnSaved -= File_OnSaved;
        }

        private void File_ContentsChanged()
        {
            RefreshChildren();
        }

        private void File_OnSaved()
        {
            NotifyChanged();
        }

        protected override IEnumerable<object> GetChildren()
        {
            yield return File.Metadata;
            foreach (var section in File.AllSections)
                yield return section;
        }

        public override string Description => File.Path is null ? "unsaved construction file" : System.IO.Path.GetFileName(File.Path);

        public override bool CanCopy => File.Path is not null;
        public override DataObject Copy()
        {
            var data = new DataObject();
            if (File.Path is not null)
                data.SetFileDropList(new StringCollection { File.Path });
            return data;
        }
        public override bool CanCut => File.Path is not null;
        public override DataObject Cut() => FileNodeOperations.Cut(File.Path);
        public override bool CanDelete => true;
        public override void Delete()
        {
            FileNodeOperations.DeleteFile(File.Path);
            base.Delete();
        }
        public override bool CanEdit => false;
        public override bool CanPaste => false;
        public override bool CanRename => File.Path is not null;
        public override bool CanSort => false;
    }

    public class ConstructionMetadataNode : ModelNode<NbtTag>
    {
        public readonly ConstructionMetadata Metadata;
        public ConstructionMetadataNode(NbtTreeModel tree, INode parent, ConstructionMetadata metadata) : base(tree, parent)
        {
            Metadata = metadata;
            Metadata.Data.OnChanged += Data_Changed;
            Metadata.Data.ActionPerformed += Data_ActionPerformed;
        }

        protected override void SelfDispose()
        {
            Metadata.Data.OnChanged -= Data_Changed;
            Metadata.Data.ActionPerformed -= Data_ActionPerformed;
        }

        private void Data_ActionPerformed(UndoableAction action)
        {
            NoticeAction(action);
        }

        private void Data_Changed(NbtTag tag)
        {
            if (Metadata.Data == tag)
                RefreshChildren();
        }

        protected override IEnumerable<NbtTag> GetChildren()
        {
            return Metadata.Data;
        }

        public override string Description => "construction metadata";

        public override bool CanCopy => true;
        public override DataObject Copy() => NbtNodeOperations.Copy(Metadata.Data);
        public override bool CanDelete => false;
        public override bool CanEdit => true;
        public override bool CanPaste => NbtNodeOperations.CanPaste(Metadata.Data);
        public override bool CanRename => false;
        public override bool CanSort => NbtNodeOperations.CanSort(Metadata.Data);
        public override void Sort() => NbtNodeOperations.Sort(Metadata.Data);
        public override IEnumerable<INode> Paste(IDataObject data)
        {
            var tags = NbtNodeOperations.Paste(Metadata.Data, data);
            return NodeChildren(tags);
        }
        public override bool CanReceiveDrop(IEnumerable<INode> nodes) => nodes.All(x => x is NbtTagNode) && NbtNodeOperations.CanReceiveDrop(Metadata.Data, nodes.Filter(x => x.GetNbtTag()));
        public override void ReceiveDrop(IEnumerable<INode> nodes, int index)
        {
            var tags = nodes.Filter(x => x.GetNbtTag());
            NbtNodeOperations.ReceiveDrop(Metadata.Data, tags, index);
        }
    }

    public class ConstructionSectionNode : ModelNode<NbtTag>
    {
        public readonly ConstructionSection Section;
        private bool HasSetupEvents = false;
        public ConstructionSectionNode(NbtTreeModel tree, INode parent, ConstructionSection section) : base(tree, parent)
        {
            Section = section;
            if (Section.IsLoaded)
                SetupEvents();
            else
                Section.OnLoaded += Section_OnLoaded;
        }

        private void Section_OnLoaded(object sender, EventArgs e)
        {
            SetupEvents();
        }

        private void SetupEvents()
        {
            if (!HasSetupEvents && Section.IsLoaded)
            {
                Section.Data.OnChanged += Data_Changed;
                Section.Data.ActionPerformed += Data_ActionPerformed;
                HasSetupEvents = true;
            }
        }

        protected override void SelfDispose()
        {
            if (HasSetupEvents)
            {
                Section.Data.OnChanged -= Data_Changed;
                Section.Data.ActionPerformed -= Data_ActionPerformed;
            }
            Section.OnLoaded -= Section_OnLoaded;
        }

        public NbtCompound AccessSectionData()
        {
            if (!Section.IsLoaded && !Section.IsCorrupt)
                Section.Load();
            return Section.Data;
        }

        public override bool HasChildren
        {
            get
            {
                if (Section.IsCorrupt)
                    return false;
                if (!Section.IsLoaded)
                    return true;
                return base.HasChildren;
            }
        }

        private void Data_ActionPerformed(UndoableAction action)
        {
            NoticeAction(action);
        }

        private void Data_Changed(NbtTag tag)
        {
            if (Section.Data == tag)
                RefreshChildren();
        }

        protected override IEnumerable<NbtTag> GetChildren()
        {
            var data = AccessSectionData();
            if (data is null)
                return Enumerable.Empty<NbtTag>();
            return data;
        }

        public override string Description => NbtUtil.ConstructionSectionDescription(Section);

        public override bool CanCopy => !Section.IsCorrupt;
        public override DataObject Copy()
        {
            var data = AccessSectionData();
            return data is null ? null : NbtNodeOperations.Copy(data);
        }
        public override bool CanDelete => false;
        public override bool CanEdit => !Section.IsCorrupt;
        public override bool CanPaste => !Section.IsCorrupt && AccessSectionData() is not null && NbtNodeOperations.CanPaste(AccessSectionData());
        public override bool CanRename => false;
        public override bool CanSort => !Section.IsCorrupt && AccessSectionData() is not null;
        public override void Sort()
        {
            var data = AccessSectionData();
            if (data is not null)
                NbtNodeOperations.Sort(data);
        }
        public override IEnumerable<INode> Paste(IDataObject data)
        {
            var section_data = AccessSectionData();
            if (section_data is null)
                return Enumerable.Empty<INode>();
            var tags = NbtNodeOperations.Paste(section_data, data);
            return NodeChildren(tags);
        }
        public override bool CanReceiveDrop(IEnumerable<INode> nodes) => !Section.IsCorrupt && nodes.All(x => x is NbtTagNode) && NbtNodeOperations.CanReceiveDrop(AccessSectionData(), nodes.Filter(x => x.GetNbtTag()));
        public override void ReceiveDrop(IEnumerable<INode> nodes, int index)
        {
            var tags = nodes.Filter(x => x.GetNbtTag());
            NbtNodeOperations.ReceiveDrop(AccessSectionData(), tags, index);
        }
    }
}
