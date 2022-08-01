using System;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Dto.Shared
{
    public class Address : IHasLatitudeLongitude
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string StateProvince { get; set; }
        public string CountryCode { get; set; }
        public string County { get; set; }
        public string PostalCode { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class GeoBoundingBox
    {
        private double _southEastLatitude;
        private double _southWestLatitude;
        private double _southEastLongitude;
        private double _northEastLongitude;
        private double _northWestLatitude;
        private double _northEastLatitude;
        private double _northWestLongitude;
        private double _southWestLongitude;

        public double SouthEastLatitude
        {
            get => Math.Abs(_southEastLatitude) > 0
                       ? _southEastLatitude
                       : _southWestLatitude;
            set => _southEastLatitude = value;
        }

        public double SouthEastLongitude
        {
            get => Math.Abs(_southEastLongitude) > 0
                       ? _southEastLongitude
                       : _northEastLongitude;
            set => _southEastLongitude = value;
        }

        public double SouthWestLatitude
        {
            get => Math.Abs(_southWestLatitude) > 0
                       ? _southWestLatitude
                       : _southEastLatitude;
            set => _southWestLatitude = value;
        }

        public double SouthWestLongitude
        {
            get => Math.Abs(_southWestLongitude) > 0
                       ? _southWestLongitude
                       : _northWestLongitude;
            set => _southWestLongitude = value;
        }

        public double NorthEastLatitude
        {
            get => Math.Abs(_northEastLatitude) > 0
                       ? _northEastLatitude
                       : _northWestLatitude;
            set => _northEastLatitude = value;
        }

        public double NorthEastLongitude
        {
            get => Math.Abs(_northEastLongitude) > 0
                       ? _northEastLongitude
                       : _southEastLongitude;
            set => _northEastLongitude = value;
        }

        public double NorthWestLatitude
        {
            get => Math.Abs(_northWestLatitude) > 0
                       ? _northWestLatitude
                       : _northEastLatitude;
            set => _northWestLatitude = value;
        }

        public double NorthWestLongitude
        {
            get => Math.Abs(_northWestLongitude) > 0
                       ? _northWestLongitude
                       : _southWestLongitude;
            set => _northWestLongitude = value;
        }
    }
}
