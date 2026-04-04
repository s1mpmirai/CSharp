namespace FoodStreetAudioGuide;

internal static class MapHtmlFactory
{
    public static string Create(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        double centerLat,
        double centerLng)
    {
        return $$"""
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
  <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" crossorigin="">
  <style>
    html, body, #map { height: 100%; margin: 0; padding: 0; background: #f6f6f6; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; }
    .leaflet-control-attribution {
      font-size: 10px;
      background: rgba(255,255,255,0.88);
      padding: 0 4px;
    }
    .leaflet-popup-content-wrapper {
      border-radius: 14px;
      box-shadow: 0 10px 22px rgba(0,0,0,0.14);
    }
    .leaflet-popup-content {
      margin: 10px 12px;
      color: #1F2738;
      font-size: 13px;
      font-weight: 700;
      line-height: 1.35;
    }
    .stall-popup-sub {
      display: block;
      margin-top: 4px;
      color: #607086;
      font-size: 11px;
      font-weight: 500;
    }
  </style>
</head>
<body>
  <div id="map"></div>
  <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" crossorigin=""></script>
  <script>
    const hcmBounds = L.latLngBounds([{{minLat}}, {{minLng}}], [{{maxLat}}, {{maxLng}}]);
    const map = L.map('map', {
      zoomControl: true,
      minZoom: 11,
      maxZoom: 19,
      zoomSnap: 0.25,
      zoomDelta: 0.5,
      maxBounds: hcmBounds,
      maxBoundsViscosity: 1.0,
      preferCanvas: true
    });

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    const poiLayer = L.layerGroup().addTo(map);
    const routeLayer = L.layerGroup().addTo(map);
    const stallMarkers = new Map();
    let userMarker = null;
    let userLatLng = null;
    let selectedStallId = null;

    function clampLat(lat) { return Math.min({{maxLat}}, Math.max({{minLat}}, lat)); }
    function clampLng(lng) { return Math.min({{maxLng}}, Math.max({{minLng}}, lng)); }

    function createPopupHtml(stall) {
      const name = escapeHtml(stall.name || 'Gian hàng');
      const subtitle = escapeHtml(stall.cuisine || '');
      return `<div>${name}${subtitle ? `<span class="stall-popup-sub">${subtitle}</span>` : ''}</div>`;
    }

    function escapeHtml(text) {
      return String(text || '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
    }

    function fitToData() {
      const bounds = L.latLngBounds([]);
      if (userLatLng) bounds.extend(userLatLng);
      stallMarkers.forEach(entry => bounds.extend(entry.marker.getLatLng()));
      if (bounds.isValid()) {
        map.fitBounds(bounds.pad(0.18), { maxZoom: 16, animate: false });
      } else {
        map.setView([{{centerLat}}, {{centerLng}}], 13);
      }
    }

    function clearRoute() {
      routeLayer.clearLayers();
    }

    async function fetchRoute(stall) {
      if (!userLatLng) {
        return [[stall.lat, stall.lng]];
      }

      const url = `https://router.project-osrm.org/route/v1/driving/${userLatLng.lng},${userLatLng.lat};${stall.lng},${stall.lat}?overview=full&geometries=geojson`;
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('route_failed');
        const payload = await response.json();
        const coordinates = payload && payload.routes && payload.routes[0] && payload.routes[0].geometry && payload.routes[0].geometry.coordinates;
        if (Array.isArray(coordinates) && coordinates.length > 1) {
          return coordinates.map(point => [point[1], point[0]]);
        }
      } catch (_) {
      }

      return [
        [userLatLng.lat, userLatLng.lng],
        [stall.lat, stall.lng]
      ];
    }

    async function selectStall(stallId, fitRoute) {
      selectedStallId = stallId;
      clearRoute();
      const entry = stallMarkers.get(stallId);
      if (!entry) return;

      entry.marker.openPopup();
      const routePoints = await fetchRoute(entry.stall);
      if (routePoints.length > 1) {
        const polyline = L.polyline(routePoints, {
          color: '#EF8F2A',
          weight: 6,
          opacity: 0.95,
          lineCap: 'round',
          lineJoin: 'round'
        }).addTo(routeLayer);

        if (fitRoute) {
          map.fitBounds(polyline.getBounds().pad(0.2), { maxZoom: 16, animate: true });
        }
      } else if (fitRoute) {
        map.setView(entry.marker.getLatLng(), 16);
      }
    }

    window.foodStreetMap = {
      setStalls(stalls) {
        poiLayer.clearLayers();
        stallMarkers.clear();
        clearRoute();

        if (userMarker) {
          userMarker.addTo(poiLayer);
        }

        (stalls || []).forEach(stall => {
          if (!stall || typeof stall.lat !== 'number' || typeof stall.lng !== 'number') return;

          const marker = L.circleMarker([stall.lat, stall.lng], {
            radius: 8,
            color: '#A94022',
            weight: 2,
            fillColor: '#E35D30',
            fillOpacity: 0.95
          }).addTo(poiLayer);

          marker.bindPopup(createPopupHtml(stall), { closeButton: false, autoPan: true, offset: [0, -8] });
          marker.on('click', () => { selectStall(stall.id, true); });
          stallMarkers.set(stall.id, { stall, marker });
        });

        if (selectedStallId && stallMarkers.has(selectedStallId)) {
          selectStall(selectedStallId, false);
        }
      },

      setUserLocation(lat, lng) {
        if (typeof lat !== 'number' || typeof lng !== 'number') return;

        userLatLng = L.latLng(clampLat(lat), clampLng(lng));
        if (!userMarker) {
          userMarker = L.circleMarker(userLatLng, {
            radius: 9,
            color: '#FFFFFF',
            weight: 3,
            fillColor: '#2563EB',
            fillOpacity: 1
          }).addTo(poiLayer);
        } else {
          userMarker.setLatLng(userLatLng);
        }

        if (selectedStallId && stallMarkers.has(selectedStallId)) {
          selectStall(selectedStallId, false);
        }
      },

      focusOnUser() {
        if (userLatLng) {
          map.setView(userLatLng, 15, { animate: false });
        } else {
          fitToData();
        }
      },

      fitToData
    };

    map.setView([{{centerLat}}, {{centerLng}}], 13);
  </script>
</body>
</html>
""";
    }
}
