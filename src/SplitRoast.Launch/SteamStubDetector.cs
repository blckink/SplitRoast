using System.IO;
using System.Text;

namespace SplitRoast.Launch;

/// <summary>
/// Detects whether an executable is wrapped with Steam's DRM (the "SteamStub").
/// Wrapped binaries carry an extra PE section named <c>.bind</c> that the stub
/// runs from before the real entry point. Such games must have the Steam client
/// running to decrypt and start: launched directly without it, the stub bounces
/// the process through <c>steam://run/&lt;appid&gt;</c> and the copy we started
/// exits immediately (which is why a second, uncontrolled fullscreen copy appears
/// and our instance falls back to a test window). Knowing this up front lets the
/// engine start Steam first instead, so the game launches cleanly into the split.
/// </summary>
public static class SteamStubDetector
{
    private const int PeSignatureOffsetLocation = 0x3C;
    private const uint PeSignature = 0x0000_4550; // "PE\0\0"
    private const int CoffHeaderSize = 20;
    private const int SectionHeaderSize = 40;

    /// <summary>
    /// Returns true if <paramref name="exePath"/> has a <c>.bind</c> section, i.e.
    /// it is wrapped with Steam DRM. Any read/parse error is treated as "not
    /// protected" so a quirky binary never blocks a launch.
    /// </summary>
    public static bool IsSteamDrmProtected(string exePath)
    {
        try
        {
            using FileStream stream = File.OpenRead(exePath);
            using var reader = new BinaryReader(stream);

            stream.Seek(PeSignatureOffsetLocation, SeekOrigin.Begin);
            int peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset > stream.Length - CoffHeaderSize)
            {
                return false;
            }

            stream.Seek(peOffset, SeekOrigin.Begin);
            if (reader.ReadUInt32() != PeSignature)
            {
                return false;
            }

            // COFF file header: Machine(2), NumberOfSections(2), ... then at +16 the
            // SizeOfOptionalHeader(2). The section table follows the optional header.
            reader.ReadUInt16();                       // Machine
            ushort sectionCount = reader.ReadUInt16(); // NumberOfSections
            reader.ReadUInt32();                       // TimeDateStamp
            reader.ReadUInt32();                       // PointerToSymbolTable
            reader.ReadUInt32();                       // NumberOfSymbols
            ushort optionalHeaderSize = reader.ReadUInt16();
            reader.ReadUInt16();                       // Characteristics

            if (sectionCount == 0 || sectionCount > 96)
            {
                return false; // Implausible: bail rather than scan garbage.
            }

            long sectionTable = peOffset + 4 + CoffHeaderSize + optionalHeaderSize;
            for (int i = 0; i < sectionCount; i++)
            {
                long entry = sectionTable + (long)i * SectionHeaderSize;
                if (entry + 8 > stream.Length)
                {
                    return false;
                }

                stream.Seek(entry, SeekOrigin.Begin);
                byte[] nameBytes = reader.ReadBytes(8);
                string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                if (name == ".bind")
                {
                    return true;
                }
            }

            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
