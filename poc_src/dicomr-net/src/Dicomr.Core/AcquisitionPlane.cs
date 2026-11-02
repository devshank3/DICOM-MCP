namespace Dicomr.Core;

/// <summary>
/// Plane from the cross product of the row and column direction cosines.
/// Matches <c>load_dicom</c> in the Python package: the dominant axis of the slice normal.
/// </summary>
public static class AcquisitionPlane
{
    public const string Axial = "axial";
    public const string Coronal = "coronal";
    public const string Sagittal = "sagittal";

    public static string? FromDirectionCosines(IReadOnlyList<double> cosines)
    {
        if (cosines.Count != 6)
        {
            return null;
        }

        var rowX = cosines[0];
        var rowY = cosines[1];
        var rowZ = cosines[2];
        var colX = cosines[3];
        var colY = cosines[4];
        var colZ = cosines[5];

        var normalX = Math.Abs((rowY * colZ) - (rowZ * colY));
        var normalY = Math.Abs((rowZ * colX) - (rowX * colZ));
        var normalZ = Math.Abs((rowX * colY) - (rowY * colX));

        if (normalX >= normalY && normalX >= normalZ)
        {
            return Sagittal;
        }

        if (normalY >= normalX && normalY >= normalZ)
        {
            return Coronal;
        }

        return Axial;
    }
}
