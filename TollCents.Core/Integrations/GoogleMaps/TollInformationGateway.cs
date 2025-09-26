using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Interfaces.Maps.Routes;
using System.Text.Json;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps.Requests;
using TollCents.Core.Integrations.GoogleMaps.Utilities;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.Core.Integrations.GoogleMaps
{
    public interface ITollInformationGateway
    {
        Task<RouteInformation?> GetRouteAvoidTollInformationAsync(ByAddressRequest addressRequest);
        Task<TollRouteInformation?> GetRouteTollInformationAsync(ByAddressRequest addressRequest);
        Task<TollRouteInformation?> GetRouteTollInformationTXAsync(ByAddressRequest addressRequest);
    }

    public class TollInformationGateway : ITollInformationGateway
    {
        private readonly IRoutesDirectionsApi _routesDirectionsApi;
        private readonly ITEXpressTollPriceCalculator _texpressTollPriceCalculator;
        private readonly string _apiKey;

        public TollInformationGateway(IRoutesDirectionsApi routesDirectionsApi, IIntegrationsConfiguration configuration,
            ITEXpressTollPriceCalculator texpressTollPriceCalculator)
        {
            var apiKey = configuration?.Integrations?.GoogleMaps?.ApiKey;
            ArgumentException.ThrowIfNullOrEmpty(apiKey, nameof(configuration.Integrations.GoogleMaps.ApiKey));
            _routesDirectionsApi = routesDirectionsApi;
            _texpressTollPriceCalculator = texpressTollPriceCalculator;
            _apiKey = apiKey;
        }

        public async Task<TollRouteInformation?> GetRouteTollInformationAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .IncludeTolls(addressRequest.IncludeTollPass ?? false ? new List<string> { "US_TX_TOLLTAG" } : null, null);

            var response = await _routesDirectionsApi.QueryAsync(request);

            return await MapToTollRouteInformation(response, addressRequest.IncludeTollPass ?? false);
        }

        public async Task<TollRouteInformation?> GetRouteTollInformationTXAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .IncludeTolls(addressRequest.IncludeTollPass ?? false ? new List<string> { "US_TX_TOLLTAG" } : null, null);
            var response = await _routesDirectionsApi.QueryAsync(request);
            return await MapToTollRouteInformation(response, addressRequest.IncludeTollPass ?? false);
        }

        public async Task<RouteInformation?> GetRouteAvoidTollInformationAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .AvoidTolls();

            var response = await _routesDirectionsApi.QueryAsync(request);

            return MapToRouteInformation(response);
        }
        private async Task<TollRouteInformation?> MapToTollRouteInformation(RoutesDirectionsResponse response, bool hasTollPass)
        {
            if (response is null || response.Status != Status.Ok || !response.Routes.Any())
                return null;

            var route = response.Routes.First();
            var routeLeg = route.Legs?.FirstOrDefault();
            var distanceInMiles = route.DistanceMeters * 0.000621371 ?? 0;
            var tollPriceUnits = Convert.ToInt32(route.TravelAdvisory?.TollInfo?.EstimatedPrice?.FirstOrDefault()?.Units ?? "0");
            var tollPriceNanos = Convert.ToDouble(route.TravelAdvisory?.TollInfo?.EstimatedPrice?.FirstOrDefault()?.Nanos ?? 0) / 1000000000;
            var texpressTolls = await _texpressTollPriceCalculator.GetTEXpressTollPrice(
                routeLeg?.Steps ?? Enumerable.Empty<RouteLegStep>(),
                hasTollPass);
            return new TollRouteInformation
            {
                DistanceInMiles = distanceInMiles,
                DriveTime = new DriveTime
                {
                    Hours = route.Duration?.Hours ?? 0,
                    Minutes = route.Duration?.Minutes ?? 0
                },
                GuaranteedTollPrice = tollPriceUnits + tollPriceNanos,
                EstimatedDynamicTollPrice = texpressTolls.TotalTollPrice,
                Description = route.Description,
                HasDynamicTolls = texpressTolls.HasTollSteps,
                ProcessedAllDynamicTolls = texpressTolls.MatchedAllSegments
            };
        }

        private RouteInformation? MapToRouteInformation(RoutesDirectionsResponse response)
        {
            if (response is null || response.Status != Status.Ok || !response.Routes.Any())
                return null;

            var route = response.Routes.First();
            var distanceInMiles = route.DistanceMeters * 0.000621371 ?? 0;
            return new RouteInformation
            {
                DistanceInMiles = distanceInMiles,
                DriveTime = new DriveTime
                {
                    Hours = route.Duration?.Hours ?? 0,
                    Minutes = route.Duration?.Minutes ?? 0
                },
                Description = route.Description
            };
        }

        private static IEnumerable<RouteLegStep> GetFakeRouteResponse()
        {
            string data = @"[
                                    {
                                        ""distanceMeters"": 1483,
                                        ""staticDuration"": ""105s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""yfsgEbrznQeRBuB?cBJsAXqAf@gOvJy@n@kAjAuA`Bw@n@aAj@oAd@o@LgBLuAGwAW{@Ya@QsA}@""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8716542,
                                                ""longitude"": -96.9707383
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8838698,
                                                ""longitude"": -96.9742352
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Head north on W Walnut Hill Ln toward N Lake College Cir""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""0.9 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""2 mins""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 958,
                                        ""staticDuration"": ""86s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""esugE~g{nQOSuBtDCRY|@Qx@MxAGfDOpA]hAmAfBYVe@Xa@PeATaCLo@L_@LyAz@_ErEUHwAvA""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8838698,
                                                ""longitude"": -96.9742352
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8893558,
                                                ""longitude"": -96.98085429999999
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Turn left onto Gateway Dr""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""0.6 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""1 min""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 2006,
                                        ""staticDuration"": ""182s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""ouvgEhq|nQsEmGaAeBw@iBo@{BcAsEy@aCq@}A}AaCs@}@yAuA{AmAiAiAcAsA{FcKQc@cE{GqDgHcI}MqBaDiBwC""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8893558,
                                                ""longitude"": -96.98085429999999
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.9004364,
                                                ""longitude"": -96.9641109
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Turn right onto State Hwy 161 N""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""1.2 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""3 mins""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 513,
                                        ""staticDuration"": ""20s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""wzxgEthynQm@i@{A{B_E}GyCoEeBqCWG""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.9004364,
                                                ""longitude"": -96.9641109
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.903492,
                                                ""longitude"": -96.9600435
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Take the President George Bush Turnpike N ramp on the left\nToll road""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""0.3 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""1 min""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 1056,
                                        ""staticDuration"": ""35s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""ymygEfoxnQ_U_`@eCyEsBsE{B_Ge@sA""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.903492,
                                                ""longitude"": -96.9600435
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.909065999999996,
                                                ""longitude"": -96.9509113
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Merge onto President George Bush Tpke N\nToll road""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""0.7 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""1 min""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 4476,
                                        ""staticDuration"": ""154s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""upzgEdvvnQ@g@eAoEeDyOo@}EYgDOiDI_HFmGZqHZqDfEy]Gq@bJap@\\gD^{FPqFBqGa@chAGqGMsZ""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.909065999999996,
                                                ""longitude"": -96.9509113
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.907539799999995,
                                                ""longitude"": -96.9037743
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Take the exit onto I-635 E""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""2.8 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""3 mins""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 14674,
                                        ""staticDuration"": ""480s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""cgzgEpomnQQYIyDMcFGyLIaCa@eFQmCU}EMmDe@kG_AkJyEea@gAsHsEgZiFi]cEy[_BmJoEcU{Gsa@cAuEoAuEiJmYeCwH}A{EeH{TsIcX_FwOuAsFaAcFq@}Eg@qFUuDQyF[u\\C_\\DuOBgc@Fa`@EeQF}EV}Fp@eI`@cFh@yHX{GDgFCyJDso@PoHtBm_@PcFF_IFuPG}K?ac@DaLMkO?eLDoDPsE`@iGf@{Eb@gClBeJlAsEbBeFjAwCxAyC""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.907539799999995,
                                                ""longitude"": -96.9037743
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.9212056,
                                                ""longitude"": -96.7513503
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Keep left to continue on Interstate 635 TEXpress, follow signs for I-635 TEXpress\nToll road""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""9.1 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""8 mins""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 6355,
                                        ""staticDuration"": ""225s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""q||gE|vomQrDiHpGsNVQjAaC`IwNbLaR`GeKrD_HzI_OhE}GrFyJtK_TnEmIxQu^h@eAn@gA~GqN~AiDlCaFtBgD~@mAbGoGtBkBzCsBpFcDvFwChDsB|BcBnCaCfBmB`DcEdAmBnC}FDe@zBgElAkChB{E`CaH~BgGpByE""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.9212056,
                                                ""longitude"": -96.7513503
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8853467,
                                                ""longitude"": -96.6992764
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Take the exit onto I-635 E\nParts of this road may be closed at certain times or days""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""3.9 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""4 mins""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 1100,
                                        ""staticDuration"": ""79s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""m|ugEnqemQxDsCb@g@v@yAtAwCvBsDFEnAkCjGoLlCoFvCkFhAkCNW""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8853467,
                                                ""longitude"": -96.6992764
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.879229099999996,
                                                ""longitude"": -96.6901364
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Exit onto Estate Ln""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""0.7 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""1 min""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 66,
                                        ""staticDuration"": ""5s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""evtgEjxcmQVg@v@_A""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.879229099999996,
                                                ""longitude"": -96.6901364
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8788294,
                                                ""longitude"": -96.689616300000012
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Continue straight""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""217 ft""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""1 min""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 5484,
                                        ""staticDuration"": ""528s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""ustgEbucmQX_@@mD?cG@cDEeMKqCA{ACkDCyI?ec@@YC_CA_v@BiXAiCBwB?_IDqPFqh@CwJ?uj@@{KIk@@gQHY@uT""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8788294,
                                                ""longitude"": -96.689616300000012
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8787294,
                                                ""longitude"": -96.6309874
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Turn left onto Kingsley Rd""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""3.4 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""9 mins""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 626,
                                        ""staticDuration"": ""51s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""astgEtfxlQdb@C""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8787294,
                                                ""longitude"": -96.6309874
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8730991,
                                                ""longitude"": -96.630968799999991
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Turn right after Jack in the Box (on the right)""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""0.4 mi""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""1 min""
                                            }
                                        }
                                    },
                                    {
                                        ""distanceMeters"": 83,
                                        ""staticDuration"": ""37s"",
                                        ""polyline"": {
                                            ""encodedPolyline"": ""{osgEpfxlQ?qD""
                                        },
                                        ""startLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8730991,
                                                ""longitude"": -96.630968799999991
                                            }
                                        },
                                        ""endLocation"": {
                                            ""latLng"": {
                                                ""latitude"": 32.8730982,
                                                ""longitude"": -96.6300775
                                            }
                                        },
                                        ""navigationInstruction"": {
                                            ""instructions"": ""Turn left onto E Woodbury Dr\nDestination will be on the left""
                                        },
                                        ""localizedValues"": {
                                            ""distance"": {
                                                ""text"": ""272 ft""
                                            },
                                            ""staticDuration"": {
                                                ""text"": ""1 min""
                                            }
                                        }
                                    }
                                ]";
            return JsonSerializer.Deserialize<IEnumerable<RouteLegStep>>(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? Enumerable.Empty<RouteLegStep>();
        }
    }

    
}
