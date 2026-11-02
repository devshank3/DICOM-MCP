using FellowOakDicom;

namespace Dicomr.Dicom;

public sealed record PrivateElement(ushort Group, byte Slot, byte Offset, string Creator, string Tag);

public sealed class PrivateTagIndex
{
    private PrivateTagIndex(IReadOnlyList<PrivateElement> elements, IReadOnlyList<string> creators)
    {
        Elements = elements;
        Creators = creators;
    }

    public IReadOnlyList<PrivateElement> Elements { get; }

    public IReadOnlyList<string> Creators { get; }

    public bool HasCreator(string fragment) =>
        Creators.Any(creator => creator.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public static PrivateTagIndex Build(DicomDataset dataset)
    {
        var creators = new Dictionary<(ushort Group, byte Slot), string>();
        foreach (var item in dataset)
        {
            var tag = item.Tag;
            if (!tag.IsPrivate || tag.Element is < 0x0010 or > 0x00FF)
            {
                continue;
            }

            if (dataset.TryGetSingleValue(tag, out string creator) && !string.IsNullOrWhiteSpace(creator))
            {
                creators[(tag.Group, (byte)tag.Element)] = creator.Trim();
            }
        }

        var elements = new List<PrivateElement>();
        foreach (var item in dataset)
        {
            var tag = item.Tag;
            if (!tag.IsPrivate || tag.Element <= 0x00FF)
            {
                continue;
            }

            var slot = (byte)(tag.Element >> 8);
            var offset = (byte)(tag.Element & 0xFF);
            creators.TryGetValue((tag.Group, slot), out var creator);
            creator ??= tag.PrivateCreator?.Creator ?? "";
            elements.Add(new PrivateElement(
                tag.Group,
                slot,
                offset,
                creator,
                StandardFieldReader.FormatTag(tag)));
        }

        var names = creators.Values
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        return new PrivateTagIndex(elements, names);
    }
}
