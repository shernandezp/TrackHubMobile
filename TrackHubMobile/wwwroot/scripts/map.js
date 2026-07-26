window.trackHubMap = {
    map: null,
    clusterGroup: null,
    markers: {},
    trackLayer: null,

    initMap: function (positions) {
        if (this.map) {
            this.destroyMap();
        }

        this.map = L.map('map', {
            zoomControl: false,
            attributionControl: false
        }).setView([14.6349, -90.5069], 10);

        // Cleaner, modern tile layer (CartoDB Voyager)
        L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; <a href="https://carto.com/">CARTO</a> &copy; <a href="https://osm.org/">OSM</a>',
            maxZoom: 20,
            subdomains: 'abcd'
        }).addTo(this.map);

        // Compact attribution in bottom-left
        L.control.attribution({ position: 'bottomleft', prefix: false }).addTo(this.map);

        // Zoom control in bottom-right
        L.control.zoom({ position: 'bottomright' }).addTo(this.map);

        this.clusterGroup = L.markerClusterGroup({
            maxClusterRadius: 45,
            spiderfyOnMaxZoom: true,
            showCoverageOnHover: false,
            animate: true,
            iconCreateFunction: function (cluster) {
                var count = cluster.getChildCount();
                var size = count < 10 ? 'small' : count < 50 ? 'medium' : 'large';
                var sizes = { small: 36, medium: 44, large: 52 };
                var colors = { small: '#0078d4', medium: '#005fa3', large: '#003d6b' };
                return L.divIcon({
                    html: '<div class="cluster-icon cluster-' + size + '" style="' +
                        'background:' + colors[size] + ';' +
                        'width:' + sizes[size] + 'px;height:' + sizes[size] + 'px;' +
                        'border-radius:50%;color:#fff;display:flex;align-items:center;justify-content:center;' +
                        'font-weight:700;font-size:' + (size === 'large' ? '15' : '13') + 'px;' +
                        'box-shadow:0 3px 10px rgba(0,0,0,0.25),0 0 0 4px rgba(0,120,212,0.2);' +
                        'border:2.5px solid rgba(255,255,255,0.9);">' +
                        count + '</div>',
                    className: 'custom-cluster',
                    iconSize: L.point(sizes[size], sizes[size])
                });
            }
        });
        this.map.addLayer(this.clusterGroup);

        if (positions && positions.length > 0) {
            this.updateMarkers(positions);
        }
    },

    updateMarkers: function (positions) {
        if (!this.map || !this.clusterGroup) return;

        this.clusterGroup.clearLayers();
        this.markers = {};

        if (!positions || positions.length === 0) return;

        var bounds = [];
        for (var i = 0; i < positions.length; i++) {
            var p = positions[i];
            var marker = this._createMarker(p);
            this.clusterGroup.addLayer(marker);
            this.markers[p.transporterId] = marker;
            bounds.push([p.lat, p.lng]);
        }

        if (bounds.length === 1) {
            this.map.setView(bounds[0], 15);
        } else if (bounds.length > 1) {
            this.map.fitBounds(bounds, { padding: [50, 50] });
        }
    },

    focusSingleUnit: function (position) {
        if (!this.map) return;

        this.clusterGroup.clearLayers();
        this.markers = {};

        var marker = this._createMarker(position);
        this.clusterGroup.addLayer(marker);
        this.markers[position.transporterId] = marker;

        this.map.setView([position.lat, position.lng], 16);
        marker.openPopup();
    },

    destroyMap: function () {
        this.clearTrack();
        if (this.clusterGroup) {
            this.clusterGroup.clearLayers();
            this.clusterGroup = null;
        }
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
        this.markers = {};
    },

    // Draws a track polyline with start/end markers and fits the map to it.
    // points: [{ lat, lng, speed, dateTime }], options: { color, weight }
    drawTrack: function (points, options) {
        if (!this.map) return;

        this.clearTrack();
        if (!points || points.length === 0) return;

        options = options || {};
        var color = options.color || '#0078d4';
        var weight = options.weight || 4;

        this.trackLayer = L.layerGroup().addTo(this.map);

        var latlngs = [];
        for (var i = 0; i < points.length; i++) {
            latlngs.push([points[i].lat, points[i].lng]);
        }

        var polyline = L.polyline(latlngs, {
            color: color,
            weight: weight,
            opacity: 0.85,
            lineJoin: 'round',
            lineCap: 'round'
        });
        this.trackLayer.addLayer(polyline);

        var startMarker = L.marker(latlngs[0], {
            icon: this._trackEndpointIcon('#22c55e')
        });
        var endMarker = L.marker(latlngs[latlngs.length - 1], {
            icon: this._trackEndpointIcon('#ef4444')
        });

        if (points[0].dateTime) {
            startMarker.bindPopup(this._trackEndpointPopup(points[0]));
        }
        if (points[points.length - 1].dateTime) {
            endMarker.bindPopup(this._trackEndpointPopup(points[points.length - 1]));
        }

        this.trackLayer.addLayer(startMarker);
        this.trackLayer.addLayer(endMarker);

        if (latlngs.length === 1) {
            this.map.setView(latlngs[0], 16);
        } else {
            this.map.fitBounds(polyline.getBounds(), { padding: [40, 40] });
        }
    },

    clearTrack: function () {
        if (this.trackLayer) {
            if (this.map) {
                this.map.removeLayer(this.trackLayer);
            }
            this.trackLayer = null;
        }
    },

    _trackEndpointIcon: function (color) {
        return L.divIcon({
            className: 'custom-marker',
            html: '<div style="' +
                'width:18px;height:18px;' +
                'background:' + color + ';' +
                'border:2.5px solid rgba(255,255,255,0.95);' +
                'border-radius:50%;' +
                'box-shadow:0 2px 6px rgba(0,0,0,0.3);"></div>',
            iconSize: [18, 18],
            iconAnchor: [9, 9],
            popupAnchor: [0, -12]
        });
    },

    _trackEndpointPopup: function (p) {
        var html = '<div class="th-popup-content"><div class="th-popup-body">';
        html += '<div class="th-popup-row"><i class="fas fa-clock"></i><span>' +
            new Date(p.dateTime).toLocaleString() + '</span></div>';
        if (p.speed !== null && p.speed !== undefined) {
            html += '<div class="th-popup-row"><i class="fas fa-tachometer-alt"></i><span>' +
                Number(p.speed).toFixed(1) + ' km/h</span></div>';
        }
        html += '</div></div>';
        return html;
    },

    _createMarker: function (p) {
        var status = this._getStatus(p);
        var rotation = p.course || 0;
        var colors = {
            moving:  { bg: '#22c55e', ring: 'rgba(34,197,94,0.25)',  glow: 'rgba(34,197,94,0.4)' },
            stopped: { bg: '#ef4444', ring: 'rgba(239,68,68,0.25)',  glow: 'rgba(239,68,68,0.4)' },
            offline: { bg: '#9ca3af', ring: 'rgba(156,163,175,0.25)', glow: 'rgba(156,163,175,0.3)' }
        };
        var c = colors[status];

        var arrowSvg = p.speed > 0
            ? '<svg viewBox="0 0 24 24" width="14" height="14" style="transform:rotate(' + rotation + 'deg)">' +
              '<path d="M12 2 L18 18 L12 14 L6 18 Z" fill="white" opacity="0.95"/></svg>'
            : '<circle cx="5" cy="5" r="3.5" fill="white" opacity="0.9" xmlns="http://www.w3.org/2000/svg"/>';

        var innerCircle = '<svg viewBox="0 0 10 10" width="8" height="8">' + arrowSvg.replace(/<svg[^>]*>/, '').replace('</svg>', '') + '</svg>';
        if (p.speed > 0) {
            innerCircle = arrowSvg;
        } else {
            innerCircle = '<div style="width:7px;height:7px;border-radius:50%;background:white;opacity:0.9;"></div>';
        }

        var icon = L.divIcon({
            className: 'custom-marker',
            html: '<div style="' +
                'width:32px;height:32px;' +
                'background:' + c.bg + ';' +
                'border:2.5px solid rgba(255,255,255,0.95);' +
                'border-radius:50%;' +
                'display:flex;align-items:center;justify-content:center;' +
                'box-shadow:0 2px 8px ' + c.glow + ',0 0 0 4px ' + c.ring + ';' +
                'transition:transform 0.3s ease;">' +
                innerCircle +
                '</div>',
            iconSize: [32, 32],
            iconAnchor: [16, 16],
            popupAnchor: [0, -20]
        });

        var popup = this._buildPopup(p, status, c.bg);
        return L.marker([p.lat, p.lng], { icon: icon }).bindPopup(popup, {
            className: 'th-popup',
            maxWidth: 260,
            minWidth: 180,
            closeButton: true
        });
    },

    _getStatus: function (p) {
        var now = new Date();
        var deviceTime = new Date(p.dateTime);
        var diffHours = (now - deviceTime) / (1000 * 60 * 60);

        if (diffHours > 2) return 'offline';
        if (p.speed > 0) return 'moving';
        return 'stopped';
    },

    _buildPopup: function (p, status, color) {
        var timeDiff = this._getTimeDiff(p.dateTime);
        var statusLabels = { moving: 'In Movement', stopped: 'Stopped', offline: 'Offline' };
        var statusLabel = statusLabels[status] || status;

        var html = '<div class="th-popup-content">';
        html += '<div class="th-popup-header">';
        html += '<div class="th-popup-title">' + this._esc(p.name) + '</div>';
        html += '<span class="th-popup-badge" style="background:' + color + ';">' + statusLabel + '</span>';
        html += '</div>';

        html += '<div class="th-popup-body">';
        html += '<div class="th-popup-row"><i class="fas fa-car"></i><span>' + this._esc(p.transporterType) + '</span></div>';
        html += '<div class="th-popup-row"><i class="fas fa-tachometer-alt"></i><span>' + p.speed.toFixed(1) + ' km/h</span></div>';
        html += '<div class="th-popup-row"><i class="fas fa-clock"></i><span>' + timeDiff + '</span></div>';

        if (p.address) {
            var addr = this._esc(p.address);
            if (p.city) addr += ', ' + this._esc(p.city);
            html += '<div class="th-popup-row"><i class="fas fa-map-pin"></i><span>' + addr + '</span></div>';
        }

        if (p.ignition !== null && p.ignition !== undefined) {
            var accColor = p.ignition ? '#22c55e' : '#ef4444';
            var accText = p.ignition ? 'ON' : 'OFF';
            html += '<div class="th-popup-row"><i class="fas fa-key"></i><span>ACC: <strong style="color:' + accColor + ';">' + accText + '</strong></span></div>';
        }

        html += '</div></div>';
        return html;
    },

    _getTimeDiff: function (dateTimeStr) {
        var now = new Date();
        var dt = new Date(dateTimeStr);
        var diffMs = now - dt;
        var mins = Math.floor(diffMs / 60000);
        if (mins < 1) return 'Just now';
        if (mins < 60) return mins + ' min' + (mins > 1 ? 's' : '') + ' ago';
        var hrs = Math.floor(mins / 60);
        if (hrs < 24) return hrs + ' hr' + (hrs > 1 ? 's' : '') + ' ago';
        var days = Math.floor(hrs / 24);
        return days + ' day' + (days > 1 ? 's' : '') + ' ago';
    },

    _esc: function (str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }
};