namespace FoodStreetAudioGuide;

internal static class MapLibreHtmlFactory
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
  <link rel="stylesheet" href="https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css">
  <link rel="stylesheet" href="https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css">
  <style>
    html, body, #map {
      height: 100%;
      margin: 0;
      padding: 0;
      background: #f6f6f6;
      overflow: hidden;
    }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }
    .leaflet-popup-content-wrapper {
      border-radius: 14px;
      box-shadow: 0 12px 24px rgba(0,0,0,0.12);
    }
    .leaflet-popup-content {
      color: #1F2738;
      font-size: 13px;
      font-weight: 700;
      padding: 2px 1px;
    }
    .popup-sub {
      display: block;
      margin-top: 4px;
      color: #607086;
      font-size: 11px;
      font-weight: 500;
    }
    .stall-marker {
      width: 16px;
      height: 16px;
      border-radius: 50%;
      background: #E35D30;
      border: 3px solid #A94022;
      box-shadow: 0 8px 18px rgba(169,64,34,0.26);
    }
    .user-marker {
      width: 18px;
      height: 18px;
      border-radius: 50%;
      background: #2563EB;
      border: 4px solid #FFFFFF;
      box-shadow: 0 8px 18px rgba(37,99,235,0.25);
    }
    .marker-cluster-small,
    .marker-cluster-medium,
    .marker-cluster-large {
      background: rgba(239, 143, 42, 0.18);
    }
    .marker-cluster-small div,
    .marker-cluster-medium div,
    .marker-cluster-large div {
      background: rgba(239, 143, 42, 0.92);
      color: white;
      font-weight: 800;
    }
  </style>
</head>
<body>
  <div id="map"></div>
  <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" crossorigin=""></script>
  <script src="https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js"></script>
  <script>
    const hcmBounds = L.latLngBounds([{{minLat}}, {{minLng}}], [{{maxLat}}, {{maxLng}}]);
    const map = L.map('map', {
      zoomControl: true,
      minZoom: 12,
      maxZoom: 20,
      maxBounds: hcmBounds,
      maxBoundsViscosity: 1.0,
      preferCanvas: true
    }).setView([{{centerLat}}, {{centerLng}}], 15);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 20,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    const stallCluster = L.markerClusterGroup({
      spiderfyOnMaxZoom: true,
      showCoverageOnHover: false,
      zoomToBoundsOnClick: true,
      disableClusteringAtZoom: 18,
      maxClusterRadius: 24,
      chunkedLoading: true
    });

    const routeLayer = L.layerGroup().addTo(map);
    map.addLayer(stallCluster);

    let userMarker = null;
    let selectedStallId = null;
    const stallMarkers = new Map();
    const stallsById = new Map();

    function jitterPoint(stall, index) {
      const sourceLat = Number(stall.lat);
      const sourceLng = Number(stall.lng);
      const signature = `${stall.id || index}:${sourceLat.toFixed(6)}:${sourceLng.toFixed(6)}`;
      let hash = 0;
      for (let i = 0; i < signature.length; i += 1) {
        hash = ((hash << 5) - hash + signature.charCodeAt(i)) | 0;
      }

      const angle = ((Math.abs(hash) % 360) * Math.PI) / 180;
      const ring = 0.00003 + ((Math.abs(hash) >> 3) % 4) * 0.000018;
      return {
        lat: sourceLat + Math.sin(angle) * ring,
        lng: sourceLng + Math.cos(angle) * ring
      };
    }

    function escapeHtml(text) {
      return String(text || '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
    }

    function popupHtml(stall) {
      const cuisine = stall.cuisine ? `<span class="popup-sub">${escapeHtml(stall.cuisine)}</span>` : '';
      return `<div>${escapeHtml(stall.name || 'Gian hang')}${cuisine}</div>`;
    }

    function clearRoute() {
      routeLayer.clearLayers();
    }

    function setRoute(coordinates) {
      clearRoute();
      if (!coordinates || coordinates.length < 2) return;
      L.polyline(
        coordinates.map(point => [point[1], point[0]]),
        {
          color: '#EF8F2A',
          weight: 6,
          opacity: 0.95,
          lineCap: 'round',
          lineJoin: 'round'
        })
        .addTo(routeLayer);
    }

    async function fetchRoute(stall) {
      if (!userMarker) {
        return [[stall.lng, stall.lat]];
      }

      const user = userMarker.getLatLng();
      const url = `https://router.project-osrm.org/route/v1/driving/${user.lng},${user.lat};${stall.lng},${stall.lat}?overview=full&geometries=geojson`;
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('route_failed');
        const payload = await response.json();
        const coordinates = payload && payload.routes && payload.routes[0] && payload.routes[0].geometry && payload.routes[0].geometry.coordinates;
        if (Array.isArray(coordinates) && coordinates.length > 1) {
          return coordinates;
        }
      } catch (_) {
      }

      return [
        [user.lng, user.lat],
        [stall.lng, stall.lat]
      ];
    }

    async function selectStall(stallId, fitRoute) {
      selectedStallId = stallId;
      const stall = stallsById.get(stallId);
      const marker = stallMarkers.get(stallId);
      if (!stall || !marker) return;

      stallCluster.zoomToShowLayer(marker, () => {
        marker.openPopup();
      });
      const route = await fetchRoute(stall);
      setRoute(route);

      if (fitRoute && route.length > 1) {
        const bounds = L.latLngBounds(route.map(point => [point[1], point[0]]));
        map.fitBounds(bounds.pad(0.15), { maxZoom: 18, animate: true, duration: 0.7 });
      } else if (fitRoute) {
        map.flyTo([stall.lat, stall.lng], 18, { duration: 0.7 });
      }
    }

    function clearStallMarkers() {
      stallCluster.clearLayers();
      stallMarkers.clear();
      stallsById.clear();
      clearRoute();
    }

    function fitToData() {
      const bounds = L.latLngBounds([]);
      let hasPoint = false;
      stallMarkers.forEach(marker => {
        bounds.extend(marker.getLatLng());
        hasPoint = true;
      });
      if (userMarker) {
        bounds.extend(userMarker.getLatLng());
        hasPoint = true;
      }

      if (hasPoint) {
        map.fitBounds(bounds.pad(0.18), { padding: [70, 70], maxZoom: 17, animate: false });
      } else {
        map.setView([{{centerLat}}, {{centerLng}}], 15);
      }
    }

    window.foodStreetMap = {
      setStalls(stalls) {
        clearStallMarkers();
        (stalls || []).forEach((stall, index) => {
          if (!stall || typeof stall.lat !== 'number' || typeof stall.lng !== 'number') return;
          const el = document.createElement('div');
          el.className = 'stall-marker';
          const point = jitterPoint(stall, index);

          const icon = L.divIcon({
            html: el.outerHTML,
            className: '',
            iconSize: [22, 22],
            iconAnchor: [11, 22],
            popupAnchor: [0, -20]
          });

          const marker = L.marker([point.lat, point.lng], { icon });
          marker.bindPopup(popupHtml(stall), {
            closeButton: false,
            autoPan: true,
            offset: [0, -18]
          });
          marker.on('click', () => { selectStall(stall.id, true); });
          stallCluster.addLayer(marker);

          stallMarkers.set(stall.id, marker);
          stallsById.set(stall.id, stall);
        });

        if (selectedStallId && stallsById.has(selectedStallId)) {
          selectStall(selectedStallId, false);
        }
      },

      setUserLocation(lat, lng) {
        if (typeof lat !== 'number' || typeof lng !== 'number') return;
        if (!userMarker) {
          const el = document.createElement('div');
          el.className = 'user-marker';
          const icon = L.divIcon({
            html: el.outerHTML,
            className: '',
            iconSize: [24, 24],
            iconAnchor: [12, 12]
          });
          userMarker = L.marker([lat, lng], { icon }).addTo(map);
        } else {
          userMarker.setLatLng([lat, lng]);
        }

        if (selectedStallId && stallsById.has(selectedStallId)) {
          selectStall(selectedStallId, false);
        }
      },

      focusOnUser() {
        if (userMarker) {
          map.flyTo(userMarker.getLatLng(), 18, { duration: 0.7 });
        } else {
          fitToData();
        }
      },

      fitToData
    };

    setTimeout(() => fitToData(), 0);
  </script>
</body>
</html>
""";
    }
}
