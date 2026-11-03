using fNbt;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TryashtarUtils.Utility;

namespace NbtStudio
{
    // A construction file is a sequence of gzip-compressed NBT compounds:
    //   "constrct" magic, format version, section data entries, metadata,
    //   metadata offset (uint32 BE), and another "constrct" magic at the end.
    // See https://github.com/Amulet-Team/construction-specification
    public class ConstructionFile : IFile
    {
        public const string MagicNumber = "constrct";
        public const byte SupportedFormatVersion = 0;
        public const int SectionIndexEntrySize = 23;

        public string Path { get; private set; }
        public byte FormatVersion { get; private set; }
        public ConstructionMetadata Metadata { get; private set; }
        public IReadOnlyList<ConstructionSection> Sections => SectionList;

        private List<ConstructionSection> SectionList = new();
        private long MetadataOffset;
        private bool SectionIndexTableBigEndian;

        public event Action OnSaved;
        public event Action ContentsChanged;

        public bool HasUnsavedChanges => Metadata is not null && (Metadata.HasUnsavedChanges || SectionList.Any(x => x.HasUnsavedChanges));
        public bool CanSave => Path is not null;
        public bool CanRefresh => CanSave;

        public ConstructionFile(string path)
        {
            Path = path;
            Load();
        }

        public static Failable<ConstructionFile> TryCreate(string path)
        {
            return new Failable<ConstructionFile>(() => new ConstructionFile(path), "Load as construction file");
        }

        private void Load()
        {
            if (Path is null)
                throw new InvalidOperationException("Cannot load a construction file without a path");

            using (var stream = File.OpenRead(Path))
            {
                long file_length = stream.Length;
                if (file_length < 9 + 12)
                    throw new FormatException("File is too short to be a construction file");

                byte[] header = new byte[9];
                ReadExactly(stream, header, 0, header.Length);
                if (Encoding.ASCII.GetString(header, 0, 8) != MagicNumber)
                    throw new FormatException($"File does not begin with the '{MagicNumber}' magic number");
                FormatVersion = header[8];
                if (FormatVersion != SupportedFormatVersion)
                    throw new FormatException($"Construction format version {FormatVersion} is not supported");

                long footer_start = file_length - 12;
                stream.Seek(footer_start, SeekOrigin.Begin);
                byte[] footer = new byte[12];
                ReadExactly(stream, footer, 0, footer.Length);
                if (Encoding.ASCII.GetString(footer, 4, 8) != MagicNumber)
                    throw new FormatException($"File does not end with the '{MagicNumber}' magic number (it may be truncated)");

                uint metadata_offset = BinaryPrimitives.ReadUInt32BigEndian(footer.AsSpan(0, 4));
                if (metadata_offset < header.Length || metadata_offset > footer_start)
                    throw new FormatException($"Invalid metadata offset {metadata_offset}");
                MetadataOffset = metadata_offset;

                int metadata_length = checked((int)(footer_start - metadata_offset));
                if (metadata_length <= 0)
                    throw new FormatException("Construction file has no metadata");

                byte[] metadata_bytes = new byte[metadata_length];
                stream.Seek(metadata_offset, SeekOrigin.Begin);
                ReadExactly(stream, metadata_bytes, 0, metadata_bytes.Length);

                var metadata_root = ReadNbt(metadata_bytes, "metadata") as NbtCompound;
                if (metadata_root is null)
                    throw new FormatException("Construction metadata is not a TAG_Compound");
                metadata_root.Name = "";
                Metadata = new ConstructionMetadata(metadata_root);

                ReadSectionIndexTable(metadata_root);
            }
        }

        private void ReadSectionIndexTable(NbtCompound metadata)
        {
            var table = metadata["section_index_table"] as NbtByteArray;
            if (table is null)
                throw new FormatException("Construction metadata is missing the 'section_index_table' TAG_Byte_Array");

            byte[] bytes = table.Value;
            if (bytes.Length % SectionIndexEntrySize != 0)
                throw new FormatException($"'section_index_table' length {bytes.Length} is not a multiple of {SectionIndexEntrySize}");

            // The specification says the table is big-endian, but the reference
            // Python implementation writes it using native (little-endian) ints.
            // Try little-endian first, then fall back to big-endian.
            bool little_endian = TryReadSectionIndexTable(bytes, big_endian: false, out var sections, out string error);
            if (!little_endian && !TryReadSectionIndexTable(bytes, big_endian: true, out sections, out error))
                throw new FormatException(error);

            SectionIndexTableBigEndian = !little_endian;
            SectionList = sections;
        }

        private bool TryReadSectionIndexTable(byte[] bytes, bool big_endian, out List<ConstructionSection> sections, out string error)
        {
            sections = new List<ConstructionSection>();
            error = null;
            for (int i = 0; i < bytes.Length; i += SectionIndexEntrySize)
            {
                var entry = bytes.AsSpan(i, SectionIndexEntrySize);
                int x = big_endian ? BinaryPrimitives.ReadInt32BigEndian(entry.Slice(0, 4)) : BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(0, 4));
                int y = big_endian ? BinaryPrimitives.ReadInt32BigEndian(entry.Slice(4, 4)) : BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(4, 4));
                int z = big_endian ? BinaryPrimitives.ReadInt32BigEndian(entry.Slice(8, 4)) : BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(8, 4));
                byte size_x = entry[12];
                byte size_y = entry[13];
                byte size_z = entry[14];
                uint offset = big_endian ? BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(15, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(15, 4));
                uint length = big_endian ? BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(19, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(19, 4));

                if (length == 0)
                {
                    error = $"Section at ({x}, {y}, {z}) has zero length";
                    sections = null;
                    return false;
                }
                if (offset < 9 || (long)offset + length > MetadataOffset)
                {
                    error = $"Section at ({x}, {y}, {z}) points outside of the section data table";
                    sections = null;
                    return false;
                }

                sections.Add(new ConstructionSection(this, x, y, z, size_x, size_y, size_z, offset, length));
            }
            return true;
        }

        public IEnumerable<ConstructionSection> AllSections => SectionList;

        internal byte[] ReadSectionBytes(ConstructionSection section)
        {
            using (var stream = File.OpenRead(Path))
            {
                return section.ReadRawBytes(stream);
            }
        }

        public void Save()
        {
            if (Path is null)
                throw new InvalidOperationException("Cannot save a construction file without a path");
            SaveTo(Path);
        }

        public void SaveAs(string path)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));
            SaveTo(path);
        }

        private void SaveTo(string destination)
        {
            string source_path = Path;
            bool needs_source = SectionList.Any(x => !x.IsLoaded);
            if (needs_source && source_path is null)
                throw new InvalidOperationException("Cannot save unloaded sections without an existing file");

            string temp_path = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var layout = new List<(uint offset, uint length)>(SectionList.Count);
            try
            {
                using (var source = needs_source ? File.OpenRead(source_path) : null)
                using (var writer = new FileStream(temp_path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    writer.Write(Encoding.ASCII.GetBytes(MagicNumber), 0, 8);
                    writer.WriteByte(SupportedFormatVersion);

                    foreach (var section in SectionList)
                    {
                        long offset = writer.Position;
                        byte[] bytes = section.IsLoaded ? section.SaveBytes() : section.ReadRawBytes(source);
                        if ((ulong)offset > uint.MaxValue)
                            throw new FormatException("Construction file is too large to be saved (section offset exceeds uint32)");
                        layout.Add(((uint)offset, checked((uint)bytes.Length)));
                        writer.Write(bytes, 0, bytes.Length);
                    }

                    var metadata = CreateMetadataForSave(layout);
                    byte[] metadata_bytes = SaveNbt(metadata);
                    long metadata_offset = writer.Position;
                    if ((ulong)metadata_offset > uint.MaxValue)
                        throw new FormatException("Construction file is too large to be saved (metadata offset exceeds uint32)");
                    writer.Write(metadata_bytes, 0, metadata_bytes.Length);

                    byte[] offset_bytes = new byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(offset_bytes, (uint)metadata_offset);
                    writer.Write(offset_bytes, 0, offset_bytes.Length);
                    writer.Write(Encoding.ASCII.GetBytes(MagicNumber), 0, 8);
                }

                File.Move(temp_path, destination, overwrite: true);
                Path = destination;
                for (int i = 0; i < SectionList.Count; i++)
                {
                    SectionList[i].Offset = layout[i].offset;
                    SectionList[i].Length = layout[i].length;
                }
                Metadata?.MarkSaved();
                foreach (var section in SectionList)
                    section.MarkSaved();
                OnSaved?.Invoke();
            }
            catch
            {
                if (File.Exists(temp_path))
                {
                    try { File.Delete(temp_path); }
                    catch { }
                }
                throw;
            }
        }

        private NbtCompound CreateMetadataForSave(List<(uint offset, uint length)> layout)
        {
            var result = (NbtCompound)Metadata.Data.Clone();
            result.Name = "";
            byte[] table = CreateSectionIndexTable(layout);
            if (result["section_index_table"] is NbtByteArray existing)
                existing.Value = table;
            else
            {
                if (result["section_index_table"] is not null)
                    result.Remove("section_index_table");
                result.Add(new NbtByteArray("section_index_table", table));
            }
            return result;
        }

        // Preserve coordinates and dimensions from the metadata table (in case the user
        // edited them) but always regenerate the offsets and lengths to match the new
        // physical layout of the section data table.
        private byte[] CreateSectionIndexTable(List<(uint offset, uint length)> layout)
        {
            byte[] existing = (Metadata.Data["section_index_table"] as NbtByteArray)?.Value;
            int existing_entries = existing is null ? 0 : existing.Length / SectionIndexEntrySize;
            byte[] result = new byte[SectionList.Count * SectionIndexEntrySize];
            for (int i = 0; i < SectionList.Count; i++)
            {
                var section = SectionList[i];
                var entry = result.AsSpan(i * SectionIndexEntrySize, SectionIndexEntrySize);
                if (i < existing_entries)
                    existing.AsSpan(i * SectionIndexEntrySize, 15).CopyTo(entry);
                else
                {
                    if (SectionIndexTableBigEndian)
                    {
                        BinaryPrimitives.WriteInt32BigEndian(entry.Slice(0, 4), section.X);
                        BinaryPrimitives.WriteInt32BigEndian(entry.Slice(4, 4), section.Y);
                        BinaryPrimitives.WriteInt32BigEndian(entry.Slice(8, 4), section.Z);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(0, 4), section.X);
                        BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(4, 4), section.Y);
                        BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(8, 4), section.Z);
                    }
                    entry[12] = section.SizeX;
                    entry[13] = section.SizeY;
                    entry[14] = section.SizeZ;
                }
                if (SectionIndexTableBigEndian)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(entry.Slice(15, 4), layout[i].offset);
                    BinaryPrimitives.WriteUInt32BigEndian(entry.Slice(19, 4), layout[i].length);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(15, 4), layout[i].offset);
                    BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(19, 4), layout[i].length);
                }
            }
            return result;
        }

        internal static NbtTag ReadNbt(byte[] bytes, string description)
        {
            var file = new fNbt.NbtFile();
            file.LoadFromBuffer(bytes, 0, bytes.Length, NbtCompression.AutoDetect);
            if (file.RootTag is null)
                throw new FormatException($"Construction {description} NBT has no root tag");
            return file.RootTag;
        }

        internal static byte[] SaveNbt(NbtTag root)
        {
            var file = new fNbt.NbtFile
            {
                RootTag = root
            };
            return file.SaveToBuffer(NbtCompression.GZip);
        }

        internal static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);
                if (read <= 0)
                    throw new EndOfStreamException();
                offset += read;
                count -= read;
            }
        }

        public void Refresh()
        {
            SectionList = new List<ConstructionSection>();
            Metadata = null;
            Load();
            ContentsChanged?.Invoke();
        }

        public void Move(string path)
        {
            if (Path is not null)
            {
                File.Move(Path, path);
                Path = path;
            }
        }
    }

    public class ConstructionMetadata
    {
        public NbtCompound Data { get; }
        public bool HasUnsavedChanges { get; private set; }

        public ConstructionMetadata(NbtCompound data)
        {
            Data = data;
            Data.OnChanged += _ => HasUnsavedChanges = true;
        }

        internal void MarkSaved()
        {
            HasUnsavedChanges = false;
        }
    }

    public class ConstructionSection
    {
        public ConstructionFile File { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public byte SizeX { get; }
        public byte SizeY { get; }
        public byte SizeZ { get; }
        public uint Offset { get; internal set; }
        public uint Length { get; internal set; }
        public NbtCompound Data { get; private set; }
        public bool IsLoaded => Data is not null;
        public bool IsCorrupt { get; private set; }
        public bool HasUnsavedChanges { get; private set; }
        public event EventHandler OnLoaded;

        internal ConstructionSection(ConstructionFile file, int x, int y, int z, byte size_x, byte size_y, byte size_z, uint offset, uint length)
        {
            File = file;
            X = x;
            Y = y;
            Z = z;
            SizeX = size_x;
            SizeY = size_y;
            SizeZ = size_z;
            Offset = offset;
            Length = length;
        }

        internal byte[] ReadRawBytes(Stream source)
        {
            source.Seek(Offset, SeekOrigin.Begin);
            byte[] bytes = new byte[checked((int)Length)];
            ConstructionFile.ReadExactly(source, bytes, 0, bytes.Length);
            return bytes;
        }

        public byte[] SaveBytes()
        {
            if (!IsLoaded)
                throw new InvalidOperationException("Cannot save a section that has not been loaded");
            return ConstructionFile.SaveNbt(Data);
        }

        public void Load()
        {
            if (IsLoaded || IsCorrupt)
                return;
            try
            {
                byte[] bytes = File.ReadSectionBytes(this);
                var root = ConstructionFile.ReadNbt(bytes, "section") as NbtCompound;
                if (root is null)
                    throw new FormatException("Construction section is not a TAG_Compound");
                root.Name = "";
                SetData(root);
            }
            catch
            {
                IsCorrupt = true;
            }
            OnLoaded?.Invoke(this, EventArgs.Empty);
        }

        private void SetData(NbtCompound data)
        {
            Data = data;
            Data.OnChanged += _ => HasUnsavedChanges = true;
        }

        internal void MarkSaved()
        {
            HasUnsavedChanges = false;
        }
    }
}
