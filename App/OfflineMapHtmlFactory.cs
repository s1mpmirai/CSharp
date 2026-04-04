namespace FoodStreetAudioGuide;

internal static class OfflineMapHtmlFactory
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
  <style>
    :root {
      --bg: #f7f1ea;
      --road: #d9c7a6;
      --road-2: #e7d8bb;
      --grid: rgba(122, 102, 78, 0.08);
      --poi: #e35d30;
      --poi-stroke: #a94022;
      --route: #ef8f2a;
      --user: #2563eb;
      --text: #1f2738;
      --sub: #607086;
    }

    html, body {
      height: 100%;
      margin: 0;
      padding: 0;
      overflow: hidden;
      background: var(--bg);
    }

    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }

    #map {
      position: relative;
      width: 100%;
      height: 100%;
      overflow: hidden;
      background:
        linear-gradient(var(--grid) 1px, transparent 1px),
        linear-gradient(90deg, var(--grid) 1px, transparent 1px),
        radial-gradient(circle at 20% 15%, rgba(255,255,255,0.8), transparent 26%),
        linear-gradient(180deg, #faf5ef 0%, #f4ede5 100%);
      background-size: 36px 36px, 36px 36px, auto, auto;
    }

    #viewport {
      position: absolute;
      inset: 0;
      transform-origin: center center;
    }

    .district-hint {
      position: absolute;
      color: rgba(86, 88, 94, 0.26);
      font-weight: 800;
      letter-spacing: 0.04em;
      pointer-events: none;
      user-select: none;
      transform: translate(-50%, -50%);
    }

    .road {
      position: absolute;
      border-radius: 999px;
      transform-origin: center center;
      opacity: 0.9;
      pointer-events: none;
    }

    .road.major {
      background: var(--road);
      box-shadow: 0 0 0 3px rgba(255,255,255,0.75) inset;
    }

    .road.minor {
      background: var(--road-2);
      opacity: 0.8;
    }

    #routeCanvas {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      pointer-events: none;
    }

    #poiLayer {
      position: absolute;
      inset: 0;
    }

    .poi, .user {
      position: absolute;
      transform: translate(-50%, -100%);
      cursor: pointer;
      touch-action: manipulation;
      background: transparent;
      border: 0;
      padding: 0;
    }

    .poi-dot {
      width: 18px;
      height: 18px;
      border-radius: 50%;
      background: var(--poi);
      border: 3px solid var(--poi-stroke);
      box-shadow: 0 8px 18px rgba(169,64,34,0.24);
      display: block;
    }

    .poi.active .poi-dot {
      transform: scale(1.15);
      box-shadow: 0 10px 22px rgba(239,143,42,0.36);
      border-color: var(--route);
    }

    .user-dot {
      width: 18px;
      height: 18px;
      border-radius: 50%;
      background: var(--user);
      border: 4px solid #ffffff;
      box-shadow: 0 8px 18px rgba(37,99,235,0.24);
      display: block;
    }

    .popup {
      position: absolute;
      left: 50%;
      bottom: calc(100% + 10px);
      transform: translateX(-50%);
      min-width: 120px;
      max-width: 180px;
      padding: 10px 12px;
      border-radius: 14px;
      background: rgba(255,255,255,0.96);
      color: var(--text);
      font-size: 13px;
      font-weight: 700;
      line-height: 1.35;
      box-shadow: 0 12px 22px rgba(0,0,0,0.14);
      display: none;
      text-align: center;
      pointer-events: none;
    }

    .poi.active .popup {
      display: block;
    }

    .popup-sub {
      display: block;
      margin-top: 4px;
      color: var(--sub);
      font-size: 11px;
      font-weight: 500;
    }

    #controls {
      position: absolute;
      left: 14px;
      top: 14px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      z-index: 10;
    }

    .ctrl {
      width: 38px;
      height: 38px;
      border: 0;
      border-radius: 12px;
      background: rgba(255,255,255,0.94);
      color: var(--text);
      font-size: 24px;
      font-weight: 700;
      box-shadow: 0 8px 18px rgba(0,0,0,0.12);
    }

    .ctrl:active {
      transform: scale(0.97);
    }

    #legend {
      position: absolute;
      right: 14px;
      bottom: 14px;
      padding: 8px 10px;
      border-radius: 12px;
      background: rgba(255,255,255,0.92);
      color: var(--sub);
      font-size: 11px;
      font-weight: 600;
      box-shadow: 0 8px 18px rgba(0,0,0,0.1);
    }
  </style>
</head>
<body>
  <div id="map">
    <div id="viewport">
      <canvas id="routeCanvas"></canvas>
      <div class="district-hint" style="left: 40%; top: 34%; font-size: 28px;">Quan 5</div>
      <div class="district-hint" style="left: 63%; top: 28%; font-size: 24px;">Quan 1</div>
      <div class="district-hint" style="left: 61%; top: 57%; font-size: 26px;">Quan 4</div>
      <div class="district-hint" style="left: 32%; top: 62%; font-size: 24px;">Quan 8</div>
      <div class="road major" style="left: 14%; top: 53%; width: 72%; height: 16px; transform: rotate(-10deg);"></div>
      <div class="road major" style="left: 22%; top: 33%; width: 58%; height: 14px; transform: rotate(18deg);"></div>
      <div class="road major" style="left: 52%; top: 16%; width: 16px; height: 60%; transform: rotate(10deg);"></div>
      <div class="road minor" style="left: 18%; top: 22%; width: 54%; height: 10px; transform: rotate(-32deg);"></div>
      <div class="road minor" style="left: 10%; top: 72%; width: 66%; height: 10px; transform: rotate(8deg);"></div>
      <div class="road minor" style="left: 32%; top: 12%; width: 10px; height: 72%; transform: rotate(-6deg);"></div>
      <div id="poiLayer"></div>
    </div>
    <div id="controls">
      <button class="ctrl" onclick="window.foodStreetMap.zoomIn()">+</button>
      <button class="ctrl" onclick="window.foodStreetMap.zoomOut()">-</button>
      <button class="ctrl" style="font-size:18px" onclick="window.foodStreetMap.focusOnUser()">◎</button>
    </div>
    <div id="legend">Offline map preview</div>
  </div>
  <script>
    const bounds = {
      minLat: {{minLat}},
      maxLat: {{maxLat}},
      minLng: {{minLng}},
      maxLng: {{maxLng}},
      centerLat: {{centerLat}},
      centerLng: {{centerLng}}
    };

    const mapEl = document.getElementById('map');
    const viewportEl = document.getElementById('viewport');
    const poiLayer = document.getElementById('poiLayer');
    const routeCanvas = document.getElementById('routeCanvas');
    const routeContext = routeCanvas.getContext('2d');

    let zoom = 1;
    let offsetX = 0;
    let offsetY = 0;
    let stalls = [];
    let userLocation = null;
    let selectedStallId = null;

    function clamp(value, min, max) {
      return Math.min(max, Math.max(min, value));
    }

    function project(lat, lng) {
      const width = mapEl.clientWidth || 1;
      const height = mapEl.clientHeight || 1;
      const x = ((lng - bounds.minLng) / (bounds.maxLng - bounds.minLng)) * width;
      const y = ((bounds.maxLat - lat) / (bounds.maxLat - bounds.minLat)) * height;
      return { x, y };
    }

    function updateTransform() {
      viewportEl.style.transform = `translate(${offsetX}px, ${offsetY}px) scale(${zoom})`;
    }

    function setCanvasSize() {
      routeCanvas.width = mapEl.clientWidth;
      routeCanvas.height = mapEl.clientHeight;
    }

    function clearRoute() {
      routeContext.clearRect(0, 0, routeCanvas.width, routeCanvas.height);
    }

    function routePoints(from, to) {
      const start = project(from.lat, from.lng);
      const end = project(to.lat, to.lng);
      const controlA = { x: start.x + (end.x - start.x) * 0.28, y: start.y - 40 };
      const controlB = { x: start.x + (end.x - start.x) * 0.72, y: end.y + 40 };
      return { start, controlA, controlB, end };
    }

    function drawRoute(stall) {
      clearRoute();
      if (!userLocation || !stall) return;

      const points = routePoints(userLocation, stall);
      routeContext.lineWidth = 6;
      routeContext.lineCap = 'round';
      routeContext.lineJoin = 'round';
      routeContext.strokeStyle = '#EF8F2A';
      routeContext.beginPath();
      routeContext.moveTo(points.start.x, points.start.y);
      routeContext.bezierCurveTo(points.controlA.x, points.controlA.y, points.controlB.x, points.controlB.y, points.end.x, points.end.y);
      routeContext.stroke();
    }

    function escapeHtml(text) {
      return String(text || '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
    }

    function buildPopup(stall) {
      const cuisine = stall.cuisine ? `<span class="popup-sub">${escapeHtml(stall.cuisine)}</span>` : '';
      return `<div class="popup">${escapeHtml(stall.name || 'Gian hang')}${cuisine}</div>`;
    }

    function renderMarkers() {
      poiLayer.innerHTML = '';

      stalls.forEach(stall => {
        const point = project(stall.lat, stall.lng);
        const marker = document.createElement('button');
        marker.className = `poi${stall.id === selectedStallId ? ' active' : ''}`;
        marker.style.left = `${point.x}px`;
        marker.style.top = `${point.y}px`;
        marker.innerHTML = `<span class="poi-dot"></span>${buildPopup(stall)}`;
        marker.onclick = () => {
          selectedStallId = stall.id;
          renderMarkers();
          drawRoute(stall);
          zoomToRoute(stall);
        };
        poiLayer.appendChild(marker);
      });

      if (userLocation) {
        const point = project(userLocation.lat, userLocation.lng);
        const marker = document.createElement('div');
        marker.className = 'user';
        marker.style.left = `${point.x}px`;
        marker.style.top = `${point.y}px`;
        marker.innerHTML = '<span class="user-dot"></span>';
        poiLayer.appendChild(marker);
      }
    }

    function zoomToRoute(stall) {
      if (!userLocation || !stall) return;

      const start = project(userLocation.lat, userLocation.lng);
      const end = project(stall.lat, stall.lng);
      const centerX = (start.x + end.x) / 2;
      const centerY = (start.y + end.y) / 2;
      const width = Math.abs(end.x - start.x) + 160;
      const height = Math.abs(end.y - start.y) + 200;
      const zoomX = mapEl.clientWidth / Math.max(width, 1);
      const zoomY = mapEl.clientHeight / Math.max(height, 1);
      zoom = clamp(Math.min(2.8, Math.max(1.1, Math.min(zoomX, zoomY))), 0.9, 3.2);
      offsetX = mapEl.clientWidth / 2 - centerX * zoom;
      offsetY = mapEl.clientHeight / 2 - centerY * zoom;
      updateTransform();
    }

    function fitToData() {
      if (!stalls.length && !userLocation) {
        zoom = 1;
        offsetX = 0;
        offsetY = 0;
        updateTransform();
        return;
      }

      const points = stalls.map(stall => project(stall.lat, stall.lng));
      if (userLocation) points.push(project(userLocation.lat, userLocation.lng));

      const xs = points.map(point => point.x);
      const ys = points.map(point => point.y);
      const minX = Math.min(...xs);
      const maxX = Math.max(...xs);
      const minY = Math.min(...ys);
      const maxY = Math.max(...ys);
      const width = Math.max(180, maxX - minX + 140);
      const height = Math.max(220, maxY - minY + 180);

      zoom = clamp(Math.min(mapEl.clientWidth / width, mapEl.clientHeight / height), 0.85, 2.6);
      offsetX = mapEl.clientWidth / 2 - ((minX + maxX) / 2) * zoom;
      offsetY = mapEl.clientHeight / 2 - ((minY + maxY) / 2) * zoom;
      updateTransform();
      clearRoute();
    }

    function focusOnUser() {
      if (!userLocation) {
        fitToData();
        return;
      }

      const point = project(userLocation.lat, userLocation.lng);
      zoom = 2.1;
      offsetX = mapEl.clientWidth / 2 - point.x * zoom;
      offsetY = mapEl.clientHeight / 2 - point.y * zoom;
      updateTransform();
    }

    function refresh() {
      setCanvasSize();
      renderMarkers();
      const selected = stalls.find(stall => stall.id === selectedStallId);
      drawRoute(selected);
    }

    window.foodStreetMap = {
      setStalls(nextStalls) {
        stalls = (nextStalls || []).filter(stall => stall && typeof stall.lat === 'number' && typeof stall.lng === 'number');
        refresh();
      },
      setUserLocation(lat, lng) {
        if (typeof lat !== 'number' || typeof lng !== 'number') return;
        userLocation = {
          lat: clamp(lat, bounds.minLat, bounds.maxLat),
          lng: clamp(lng, bounds.minLng, bounds.maxLng)
        };
        refresh();
      },
      focusOnUser,
      fitToData,
      zoomIn() {
        zoom = clamp(zoom + 0.22, 0.85, 3.2);
        updateTransform();
      },
      zoomOut() {
        zoom = clamp(zoom - 0.22, 0.85, 3.2);
        updateTransform();
      }
    };

    window.addEventListener('resize', () => {
      refresh();
    });

    setCanvasSize();
    fitToData();
  </script>
</body>
</html>
""";
    }
}
