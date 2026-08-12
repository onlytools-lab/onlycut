using CutPic.Models;
using System.Windows;

namespace CutPic.Tests;

public sealed class EditingAndNamingTests
{
    [Fact]
    public void ExportNamingUsesLongImagePartAndPadding()
    {
        string name = MainWindow.BuildExportFileName("商品", true, 0, ".png", 0, "", "x", 5, 3);
        Assert.Equal("商品_part_005_x.png", name);
    }

    [Fact]
    public void OppositeRotationsCancel()
    {
        var transforms = new List<ImageTransformOperation> { ImageTransformOperation.RotateLeft };
        MainWindow.AppendTransform(transforms, ImageTransformOperation.RotateRight);
        Assert.Empty(transforms);
    }

    [Fact]
    public void LongDividerOperationsKeepContinuousCoverage()
    {
        var regions = new List<CropRegion> { new() { Bounds = new Rect(0, 0, 400, 900) } };
        Assert.True(MainWindow.TryInsertLongDivider(regions, 400, 300, out int divider));
        MainWindow.MoveLongDivider(regions, 400, divider, 420);
        Assert.Equal(regions[0].Bounds.Bottom, regions[1].Bounds.Top);
        Assert.Equal(900, regions.Sum(region => region.Bounds.Height));
        MainWindow.RemoveLongDivider(regions, 400, divider);
        Assert.Single(regions);
        Assert.Equal(new Rect(0, 0, 400, 900), regions[0].Bounds);
    }
}
