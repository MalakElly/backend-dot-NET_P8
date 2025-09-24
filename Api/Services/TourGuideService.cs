using GpsUtil.Location;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using TourGuide.LibrairiesWrappers.Interfaces;
using TourGuide.Services.Interfaces;
using TourGuide.Users;
using TourGuide.Utilities;
using TripPricer;

namespace TourGuide.Services;

public class TourGuideService : ITourGuideService
{
    private readonly ILogger _logger;
    private readonly IGpsUtil _gpsUtil;
    private readonly IRewardsService _rewardsService;
    private readonly TripPricer.TripPricer _tripPricer;
    public Tracker Tracker { get; private set; }
    private readonly Dictionary<string, User> _internalUserMap = new();
    private const string TripPricerApiKey = "test-server-api-key";//TODO: A mettre dans appsettings.json
    private bool _testMode = true;

    public TourGuideService(ILogger<TourGuideService> logger, IGpsUtil gpsUtil, IRewardsService rewardsService, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _tripPricer = new();
        _gpsUtil = gpsUtil;
        _rewardsService = rewardsService;

        CultureInfo.CurrentCulture = new CultureInfo("en-US");

        if (_testMode)
        {
            _logger.LogInformation("TestMode enabled");
            _logger.LogDebug("Initializing users");
            InitializeInternalUsers();
            _logger.LogDebug("Finished initializing users");
        }

        var trackerLogger = loggerFactory.CreateLogger<Tracker>();

        Tracker = new Tracker(this, trackerLogger);
        AddShutDownHook();
    }

    public List<UserReward> GetUserRewards(User user)
    {
        return user.UserRewards;
    }

    public VisitedLocation GetUserLocation(User user)
    {
           return user.VisitedLocations.Any() ? user.GetLastVisitedLocation() : TrackUserLocationAsync(user).Result;
    }

    public User GetUser(string userName)
    {
        return _internalUserMap.ContainsKey(userName) ? _internalUserMap[userName] : null;
    }

    public List<User> GetAllUsers()
    {
        return _internalUserMap.Values.ToList();
    }

    public void AddUser(User user)
    {
        if (!_internalUserMap.ContainsKey(user.UserName))
        {
            _internalUserMap.Add(user.UserName, user);
        }
    }
    public async Task<List<Provider>> GetTripDealsAsync(User user)
    {
        int cumulativeRewardPoints = user.UserRewards.Sum(i => i.RewardPoints);

        var tripPricerTask = new TripPricerTask(
            TripPricerApiKey,
            user.UserId,
            user.UserPreferences.NumberOfAdults,
            user.UserPreferences.NumberOfChildren,
            user.UserPreferences.TripDuration
        );

        List<Provider> providers = await tripPricerTask.ExecuteAsync();
        user.TripDeals = providers;
        return providers;
    }



    //public List<Provider> GetTripDeals(User user)
    //{
    //    int cumulativeRewardPoints = user.UserRewards.Sum(i => i.RewardPoints);

    //    List<Provider> providers = _tripPricer.GetPrice(
    //        TripPricerApiKey,
    //        user.UserId,
    //        user.UserPreferences.NumberOfAdults,
    //        user.UserPreferences.NumberOfChildren,
    //        user.UserPreferences.TripDuration,
    //        cumulativeRewardPoints
    //    );


    //    while (providers.Count < 10)
    //    {
    //        providers.Add(new Provider(
    //            Guid.NewGuid().ToString(),
    //            100.0,
    //            1  // par défaut 1 jour
    //        ));
    //    }

    //    user.TripDeals = providers.Take(10).ToList();
    //    return user.TripDeals;
    //}


    public async Task<VisitedLocation> TrackUserLocationAsync(User user)
    {
        VisitedLocation visitedLocation = await Task.Run(() => _gpsUtil.GetUserLocation(user.UserId));//appel bloquant 
        user.AddToVisitedLocations(visitedLocation);

        // Calcul des rewards en parallèle aussi
        await Task.Run(() => _rewardsService.CalculateRewards(user));

        return visitedLocation;
    }

    public List<Attraction> GetNearByAttractions(VisitedLocation visitedLocation)
    {
        // Récupérer toutes les attractions
        var attractions = _gpsUtil.GetAttractions();

        // Trier par distance entre l'utilisateur et l'attraction
        var nearestAttractions = attractions
            .OrderBy(a => GetDistance(visitedLocation.Location, a))
            .Take(5) // Garder seulement les 5 plus proches
            .ToList();

        return nearestAttractions;
    }

    // Méthode utilitaire pour calculer la distance entre deux points GPS
    private double GetDistance(Locations loc1, Locations loc2)
    {
        double lat1 = loc1.Latitude;
        double lon1 = loc1.Longitude;
        double lat2 = loc2.Latitude;
        double lon2 = loc2.Longitude;

        double theta = lon1 - lon2;
        double dist = Math.Sin(Deg2Rad(lat1)) * Math.Sin(Deg2Rad(lat2)) +
                      Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) * Math.Cos(Deg2Rad(theta));
        dist = Math.Acos(dist);
        dist = Rad2Deg(dist);
        dist = dist * 60 * 1.1515; // miles
        return dist;
    }

    private double Deg2Rad(double deg) => (deg * Math.PI / 180.0);
    private double Rad2Deg(double rad) => (rad / Math.PI * 180.0);

    private void AddShutDownHook()
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => Tracker.StopTracking();
    }

    /**********************************************************************************
    * 
    * Methods Below: For Internal Testing
    * 
    **********************************************************************************/

    private void InitializeInternalUsers()
    {
        for (int i = 0; i < InternalTestHelper.GetInternalUserNumber(); i++)
        {
            var userName = $"internalUser{i}";
            var user = new User(Guid.NewGuid(), userName, "000", $"{userName}@tourGuide.com");
            GenerateUserLocationHistory(user);
            _internalUserMap.Add(userName, user);
        }

        _logger.LogDebug($"Created {InternalTestHelper.GetInternalUserNumber()} internal test users.");
    }

    private void GenerateUserLocationHistory(User user)
    {
        for (int i = 0; i < 3; i++)
        {
            var visitedLocation = new VisitedLocation(user.UserId, new Locations(GenerateRandomLatitude(), GenerateRandomLongitude()), GetRandomTime());
            user.AddToVisitedLocations(visitedLocation);
        }
    }

    private static readonly Random random = new Random();

    private double GenerateRandomLongitude()
    {
        return new Random().NextDouble() * (180 - (-180)) + (-180);
    }

    private double GenerateRandomLatitude()
    {
        return new Random().NextDouble() * (90 - (-90)) + (-90);
    }

    private DateTime GetRandomTime()
    {
        return DateTime.UtcNow.AddDays(-new Random().Next(30));
    }
}
