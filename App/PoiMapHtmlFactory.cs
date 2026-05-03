using System.Text.Json;

namespace FoodStreetAudioGuide;

internal static class PoiMapHtmlFactory
{
    public static string Create(
        IEnumerable<object> stallsPayload,
        double? userLat,
        double? userLng,
        int? nearestStallId,
        string openDetailText,
        string userLocationText,
        string routePrefixText,
        string routeUnavailableText)
    {
        var stallsJson = JsonSerializer.Serialize(stallsPayload);
        var userLatValue = userLat?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        var userLngValue = userLng?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        var nearestStallIdValue = nearestStallId?.ToString() ?? "null";
        var openDetailTextJson = JsonSerializer.Serialize(openDetailText);
        var userLocationTextJson = JsonSerializer.Serialize(userLocationText);
        var routePrefixTextJson = JsonSerializer.Serialize(routePrefixText);
        var routeUnavailableTextJson = JsonSerializer.Serialize(routeUnavailableText);

        return $$"""
<!doctype html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <style>
        html, body, #map { height: 100%; margin: 0; padding: 0; }
        body { font-family: sans-serif; background: #f6f6f6; }
        .stall-popup { min-width: 180px; }
        .stall-popup strong { display: block; margin-bottom: 4px; color: #1f2738; }
        .stall-popup span { color: #6a7483; font-size: 12px; }
        .route-hint {
            position: absolute;
            left: 12px;
            right: 12px;
            bottom: 12px;
            z-index: 500;
            background: rgba(255,255,255,.96);
            border-radius: 14px;
            padding: 10px 12px;
            box-shadow: 0 8px 18px rgba(0,0,0,.14);
            color: #1f2738;
            font-size: 13px;
            display: none;
        }
    </style>
</head>
<body>
    <div id="map"></div>
    <div id="routeHint" class="route-hint"></div>
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <script>
        const stalls = {{stallsJson}};
        const userLat = {{userLatValue}};
        const userLng = {{userLngValue}};
        const nearestStallId = {{nearestStallIdValue}};
        const openDetailText = {{openDetailTextJson}};
        const userLocationText = {{userLocationTextJson}};
        const routePrefixText = {{routePrefixTextJson}};
        const routeUnavailableText = {{routeUnavailableTextJson}};

        const HCMC_BOUNDS = L.latLngBounds(
            [10.10, 106.30],
            [11.20, 107.10]
        );

        const map = L.map('map', {
            maxBounds: HCMC_BOUNDS,
            maxBoundsViscosity: 1.0,
            minZoom: 11,
            maxZoom: 19
        });
        const bounds = [];
        let activeRoute = null;
        let activeRouteAbort = null;
        let initialNearestMarker = null;
        let initialNearestStall = null;
        const routeHint = document.getElementById('routeHint');

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors',
            maxZoom: 19
        }).addTo(map);

        const defaultIcon = new L.Icon({
            iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon.png',
            shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34],
            shadowSize: [41, 41]
        });

        const nearestIcon = new L.Icon({
            iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-orange.png',
            shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34],
            shadowSize: [41, 41]
        });

        const userIcon = new L.CircleMarker([0, 0], {
            radius: 9,
            color: '#2e7cf6',
            weight: 3,
            fillColor: '#8ec5ff',
            fillOpacity: 1
        });

        function formatRouteSummary(distanceMeters, durationSeconds) {
            const km = distanceMeters >= 1000
                ? `${(distanceMeters / 1000).toFixed(2)} km`
                : `${Math.round(distanceMeters)} m`;
            const minutes = Math.max(1, Math.round(durationSeconds / 60));
            return `${routePrefixText}: ${km} - ~${minutes} min`;
        }

        async function drawShortestRoute(stall) {
            if (userLat === null || userLng === null) {
                routeHint.style.display = 'none';
                return;
            }

            if (activeRouteAbort) {
                activeRouteAbort.abort();
            }

            if (activeRoute) {
                map.removeLayer(activeRoute);
                activeRoute = null;
            }

            activeRouteAbort = new AbortController();

            try {
                const url = `https://router.project-osrm.org/route/v1/foot/${userLng},${userLat};${stall.lng},${stall.lat}?overview=full&geometries=geojson`;
                const response = await fetch(url, { signal: activeRouteAbort.signal });
                if (!response.ok) {
                    throw new Error('route failed');
                }

                const data = await response.json();
                const route = data.routes && data.routes[0];
                if (!route || !route.geometry || !Array.isArray(route.geometry.coordinates)) {
                    throw new Error('route empty');
                }

                const latLngs = route.geometry.coordinates.map(point => [point[1], point[0]]);
                activeRoute = L.polyline(latLngs, {
                    color: '#ef8f2a',
                    weight: 5,
                    opacity: 0.9
                }).addTo(map);

                const routeBounds = L.latLngBounds([
                    [userLat, userLng],
                    [stall.lat, stall.lng],
                    ...latLngs
                ]);
                map.fitBounds(routeBounds, { padding: [48, 48], maxZoom: 17 });

                routeHint.textContent = `${stall.name}: ${formatRouteSummary(route.distance || 0, route.duration || 0)}`;
                routeHint.style.display = 'block';
            } catch (error) {
                if (error && error.name === 'AbortError') {
                    return;
                }

                routeHint.textContent = `${stall.name}: ${routeUnavailableText}`;
                routeHint.style.display = 'block';
            }
        }

        stalls.forEach(stall => {
            if (!stall.lat || !stall.lng) return;

            const marker = L.marker([stall.lat, stall.lng], {
                icon: stall.id === nearestStallId ? nearestIcon : defaultIcon
            }).addTo(map);

            if (stall.id === nearestStallId) {
                initialNearestMarker = marker;
                initialNearestStall = stall;
            }

            marker.bindPopup(`
                <div class="stall-popup">
                    <strong>${stall.name}</strong>
                    <span>${stall.cuisine || ''}</span><br />
                    <span>${stall.distanceText || ''}</span><br />
                    <a href="foodstreet://stall/${stall.id}">${openDetailText}</a>
                </div>
            `);
            marker.on('click', () => {
                drawShortestRoute(stall);
            });
            bounds.push([stall.lat, stall.lng]);
        });

        if (userLat !== null && userLng !== null) {
            userIcon.setLatLng([userLat, userLng]).addTo(map).bindPopup(userLocationText);
            bounds.push([userLat, userLng]);
        }

        if (initialNearestStall) {
            if (userLat !== null && userLng !== null) {
                map.setView([initialNearestStall.lat, initialNearestStall.lng], 18, { animate: false });
            } else {
                map.setView([initialNearestStall.lat, initialNearestStall.lng], 18, { animate: false });
            }

            if (initialNearestMarker) {
                initialNearestMarker.openPopup();
            }
        } else if (bounds.length > 0) {
            map.fitBounds(bounds, { padding: [36, 36], maxZoom: 17 });
        } else {
            map.setView([10.7626, 106.7045], 14);
        }

        map.panInsideBounds(HCMC_BOUNDS, { animate: false });
    </script>
</body>
</html>
""";
    }
}
