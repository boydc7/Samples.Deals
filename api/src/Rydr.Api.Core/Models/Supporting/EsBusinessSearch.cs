using System.Collections.Generic;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Models.Supporting
{
    public class EsBusinessSearch : IHasLatitudeLongitude
    {
        public long PublisherAccountId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Miles { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 100;
        public string Search { get; set; }
        public HashSet<Tag> Tags { get; set; }
    }
}
