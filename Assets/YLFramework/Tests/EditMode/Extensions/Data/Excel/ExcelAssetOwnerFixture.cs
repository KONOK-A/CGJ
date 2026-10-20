using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Tests.Extensions.Data
{
    public sealed class ExcelAssetOwnerFixture : ScriptableObject
    {
        public List<ExcelAssetRowFixture> Rows = new List<ExcelAssetRowFixture>();
    }
}
