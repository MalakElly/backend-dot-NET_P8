using GpsUtil.Location;
using RewardCentral;
using System.Diagnostics;
using System.Threading.Tasks;
using TourGuide.LibrairiesWrappers.Interfaces;
using TourGuide.Services.Interfaces;
using TourGuide.Users;

namespace TourGuide.Services;

public class RewardsService : IRewardsService
{
    private const double StatuteMilesPerNauticalMile = 1.15077945;
    private readonly int _defaultProximityBuffer = 10;
    private int _proximityBuffer;
    private readonly int _attractionProximityRange = 200;
    private readonly IGpsUtil _gpsUtil;
    private readonly IRewardCentral _rewardsCentral;
    private static int count = 0;

    public RewardsService(IGpsUtil gpsUtil, IRewardCentral rewardCentral)
    {
        _gpsUtil = gpsUtil;
        _rewardsCentral =rewardCentral;
        _proximityBuffer = _defaultProximityBuffer;
    }
   

    public void SetProximityBuffer(int proximityBuffer)
    {
        _proximityBuffer = proximityBuffer;
    }

    public void SetDefaultProximityBuffer()
    {
        _proximityBuffer = _defaultProximityBuffer;
    }

    public async Task CalculateRewardsAsync(User user)
    {
        List<VisitedLocation> userLocations = user.VisitedLocations;
        List<Attraction> attractions = _gpsUtil.GetAttractions();
        await Task.Run(() => 
        {  
        for (int i=0;i<userLocations.Count;i++)
        {
            
            for (int j=0;j<attractions.Count;j++)
            {
              
                var rewardsSnapshot = user.UserRewards.ToList();

                if (!rewardsSnapshot.Any(r => r.Attraction.AttractionName == attractions[j].AttractionName))//Eviter InvalideOperationException
                {
                    if (NearAttraction(userLocations[i], attractions[j]))
                    {
                        user.AddUserReward(new UserReward(
                           userLocations[i],
                            attractions[j],
                            GetRewardPoints(attractions[j], user)
                        ));
                    }
                }
            }
        }
        });
    }
    //public void CalculateRewards(User user)
    //{
    //    List<VisitedLocation> userLocations = user.VisitedLocations;
    //    List<Attraction> attractions = _gpsUtil.GetAttractions();

    //    foreach (var visitedLocation in userLocations)
    //    {
    //        Debug.WriteLine("VistedLocation");
    //        Debug.WriteLine(visitedLocation.Location.Longitude);
    //        Debug.WriteLine(visitedLocation.Location.Latitude);
    //        foreach (var attraction in attractions)
    //        {
    //            Debug.WriteLine("attraction");
    //            Debug.WriteLine(attraction.Longitude);
    //            Debug.WriteLine(attraction.Latitude);
    //            var rewardsSnapshot = user.UserRewards.ToList();

    //            if (!rewardsSnapshot.Any(r => r.Attraction.AttractionName == attraction.AttractionName))//Eviter InvalideOperationException
    //            {
    //                if (NearAttraction(visitedLocation, attraction))
    //                {
    //                    user.AddUserReward(new UserReward(
    //                        visitedLocation,
    //                        attraction,
    //                        GetRewardPoints(attraction, user)
    //                    ));
    //                }
    //            }
    //        }
    //    }
    //}




    public bool IsWithinAttractionProximity(Attraction attraction, Locations location)
    {
        Console.WriteLine(GetDistance(attraction, location));
        return GetDistance(attraction, location) <= _attractionProximityRange;
    }

    private bool NearAttraction(VisitedLocation visitedLocation, Attraction attraction)
    {
        if (visitedLocation.Location.Latitude == attraction.Latitude &&
        visitedLocation.Location.Longitude == attraction.Longitude)
        {
            return true;
        }
        return GetDistance(attraction, visitedLocation.Location) <= _proximityBuffer;
    }

    public int GetRewardPoints(Attraction attraction, User user)
    {
        return _rewardsCentral.GetAttractionRewardPoints(attraction.AttractionId, user.UserId);
    }

    public double GetDistance(Locations loc1, Locations loc2)
    {
        double lat1 = Math.PI * loc1.Latitude / 180.0;
        double lon1 = Math.PI * loc1.Longitude / 180.0;
        double lat2 = Math.PI * loc2.Latitude / 180.0;
        double lon2 = Math.PI * loc2.Longitude / 180.0;

        double angle = Math.Acos(Math.Sin(lat1) * Math.Sin(lat2)
                                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(lon1 - lon2));

        double nauticalMiles = 60.0 * angle * 180.0 / Math.PI;
        return StatuteMilesPerNauticalMile * nauticalMiles;
    }
}
