window.trackHubMap = {
    map: null,
    clusterGroup: null,
    markers: {},
    trackLayer: null,
    hasFittedView: false,
    openPopupId: null,
    labels: {
        moving: 'In Movement',
        stopped: 'Stopped',
        offline: 'Offline',
        justNow: 'Just now',
        minutesAgo: '{0} min ago',
        hoursAgo: '{0} h ago',
        daysAgo: '{0} d ago',
        accOn: 'ON',
        accOff: 'OFF'
    },

    // options: { zoomPosition, attributionPosition } — screens that overlay the bottom of
    // the map (a results sheet) move the controls out from under it.
    initMap: function (positions, labels, options) {
        if (this.map) {
            this.destroyMap();
        }

        if (labels) {
            this.labels = Object.assign({}, this.labels, labels);
        }
        options = options || {};
        this.hasFittedView = false;
        this.openPopupId = null;

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
        L.control.attribution({ position: options.attributionPosition || 'bottomleft', prefix: false }).addTo(this.map);

        // Zoom control in bottom-right
        L.control.zoom({ position: options.zoomPosition || 'bottomright' }).addTo(this.map);

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

        // Track which unit's popup is open so it survives marker rebuilds
        var self = this;
        this.map.on('popupopen', function (e) {
            if (e.popup._source && e.popup._source.options.transporterId) {
                self.openPopupId = e.popup._source.options.transporterId;
            }
        });
        this.map.on('popupclose', function (e) {
            if (e.popup._source && e.popup._source.options.transporterId === self.openPopupId) {
                self.openPopupId = null;
            }
        });

        if (positions && positions.length > 0) {
            this.updateMarkers(positions, true);
        }
    },

    // Rebuilds the markers. The view is fitted to the fleet only on the first
    // load (or when fitView is true); periodic refreshes keep the user's
    // pan/zoom and reopen the popup that was open before the rebuild.
    updateMarkers: function (positions, fitView) {
        if (!this.map || !this.clusterGroup) return;

        var reopenId = this.openPopupId;

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

        if (fitView || !this.hasFittedView) {
            if (bounds.length === 1) {
                this.map.setView(bounds[0], 15);
            } else {
                this.map.fitBounds(bounds, { padding: [50, 50] });
            }
            this.hasFittedView = true;
        }

        if (reopenId && this.markers[reopenId]) {
            this.openPopupId = reopenId;
            var m = this.markers[reopenId];
            // Only reopen when the marker is actually visible (not clustered)
            var visible = this.clusterGroup.getVisibleParent(m);
            if (visible === m) {
                m.openPopup();
            }
        }
    },

    // Shows a single unit. preserveView keeps the current pan/zoom (used by the
    // periodic refresh); otherwise the map centers on the unit and opens its popup.
    focusSingleUnit: function (position, preserveView) {
        if (!this.map) return;

        var wasOpen = this.openPopupId === position.transporterId;

        this.clusterGroup.clearLayers();
        this.markers = {};

        var marker = this._createMarker(position);
        this.clusterGroup.addLayer(marker);
        this.markers[position.transporterId] = marker;

        if (!preserveView) {
            this.map.setView([position.lat, position.lng], 16);
            this.hasFittedView = true;
            marker.openPopup();
        } else if (wasOpen) {
            marker.openPopup();
        }
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
        this.hasFittedView = false;
        this.openPopupId = null;
    },

    // Draws a track polyline with start/end markers and fits the map to it.
    // A single point renders as one stop marker.
    // points: [{ lat, lng, speed, dateTime }], options: { color, weight, bottomInsetRatio }
    drawTrack: function (points, options) {
        if (!this.map) return;

        this.clearTrack();
        if (!points || points.length === 0) return;

        options = options || {};
        this.trackLayer = L.layerGroup().addTo(this.map);

        if (points.length === 1) {
            this.trackLayer.addLayer(this._stopMarker(points[0], '#ef4444', 18));
            this.map.setView([points[0].lat, points[0].lng], 16);
            return;
        }

        var polyline = this._segmentPolyline(points, options);
        this.trackLayer.addLayer(polyline);
        this.trackLayer.addLayer(this._endpointMarker(points[0], '#22c55e'));
        this.trackLayer.addLayer(this._endpointMarker(points[points.length - 1], '#ef4444'));

        this._fitTrack(polyline.getBounds(), options);
    },

    // Draws a whole range at once: one polyline per moving segment plus a dot per stop,
    // fitted to everything. segments: [[{ lat, lng, speed, dateTime }, ...], ...]
    drawTracks: function (segments, stops, options) {
        if (!this.map) return;

        this.clearTrack();
        segments = segments || [];
        stops = stops || [];
        if (segments.length === 0 && stops.length === 0) return;

        options = options || {};
        this.trackLayer = L.layerGroup().addTo(this.map);

        var bounds = L.latLngBounds([]);
        var firstPoint = null;
        var lastPoint = null;

        for (var i = 0; i < segments.length; i++) {
            var seg = segments[i];
            if (!seg || seg.length === 0) continue;

            if (seg.length > 1) {
                var line = this._segmentPolyline(seg, options);
                this.trackLayer.addLayer(line);
                bounds.extend(line.getBounds());
            } else {
                bounds.extend([seg[0].lat, seg[0].lng]);
            }

            if (!firstPoint) firstPoint = seg[0];
            lastPoint = seg[seg.length - 1];
        }

        for (var j = 0; j < stops.length; j++) {
            this.trackLayer.addLayer(this._stopMarker(stops[j], '#f59e0b', 12));
            bounds.extend([stops[j].lat, stops[j].lng]);
        }

        if (firstPoint) {
            this.trackLayer.addLayer(this._endpointMarker(firstPoint, '#22c55e'));
        }
        if (lastPoint && lastPoint !== firstPoint) {
            this.trackLayer.addLayer(this._endpointMarker(lastPoint, '#ef4444'));
        }

        if (bounds.isValid()) {
            this._fitTrack(bounds, options);
        }
    },

    // Re-measures the container after the surrounding layout changed (a filter panel
    // opening or closing), otherwise Leaflet keeps rendering at the stale size.
    resize: function () {
        if (this.map) {
            this.map.invalidateSize();
        }
    },

    // Re-fits the drawn track, used when the space left over by an overlay changes.
    refit: function (options) {
        if (!this.map || !this.trackLayer) return;

        var bounds = L.latLngBounds([]);
        this.trackLayer.eachLayer(function (layer) {
            if (layer.getBounds) {
                bounds.extend(layer.getBounds());
            } else if (layer.getLatLng) {
                bounds.extend(layer.getLatLng());
            }
        });

        if (bounds.isValid()) {
            this._fitTrack(bounds, options || {});
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

    _segmentPolyline: function (points, options) {
        var latlngs = [];
        for (var i = 0; i < points.length; i++) {
            latlngs.push([points[i].lat, points[i].lng]);
        }
        return L.polyline(latlngs, {
            color: options.color || '#0078d4',
            weight: options.weight || 4,
            opacity: 0.85,
            lineJoin: 'round',
            lineCap: 'round'
        });
    },

    _endpointMarker: function (point, color) {
        return this._stopMarker(point, color, 18);
    },

    _stopMarker: function (point, color, size) {
        var marker = L.marker([point.lat, point.lng], {
            icon: this._trackEndpointIcon(color, size)
        });
        if (point.dateTime) {
            marker.bindPopup(this._trackEndpointPopup(point));
        }
        return marker;
    },

    // Keeps the fitted track clear of whatever overlays the bottom of the map.
    _fitTrack: function (bounds, options) {
        var inset = 40;
        if (options.bottomInsetRatio) {
            inset = Math.max(40, Math.round(this.map.getSize().y * options.bottomInsetRatio));
        }
        // A single point has no extent to fit; recentring it would jump to max zoom
        if (bounds.getNorthEast().equals(bounds.getSouthWest())) {
            this.map.setView(bounds.getCenter(), this.map.getZoom());
            return;
        }
        this.map.fitBounds(bounds, {
            paddingTopLeft: [40, 40],
            paddingBottomRight: [40, inset]
        });
    },

    _trackEndpointIcon: function (color, size) {
        size = size || 18;
        return L.divIcon({
            className: 'custom-marker',
            html: '<div style="' +
                'width:' + size + 'px;height:' + size + 'px;' +
                'background:' + color + ';' +
                'border:2.5px solid rgba(255,255,255,0.95);' +
                'border-radius:50%;' +
                'box-shadow:0 2px 6px rgba(0,0,0,0.3);"></div>',
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2],
            popupAnchor: [0, -(size / 2 + 3)]
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

        var innerCircle;
        if (p.speed > 0) {
            innerCircle = '<svg viewBox="0 0 24 24" width="14" height="14" style="transform:rotate(' + rotation + 'deg)">' +
                '<path d="M12 2 L18 18 L12 14 L6 18 Z" fill="white" opacity="0.95"/></svg>';
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
        return L.marker([p.lat, p.lng], { icon: icon, transporterId: p.transporterId }).bindPopup(popup, {
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
        var statusLabel = this.labels[status] || status;

        var html = '<div class="th-popup-content">';
        html += '<div class="th-popup-header">';
        html += '<div class="th-popup-title">' + this._esc(p.name) + '</div>';
        html += '<span class="th-popup-badge" style="background:' + color + ';">' + this._esc(statusLabel) + '</span>';
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
            var accText = p.ignition ? this.labels.accOn : this.labels.accOff;
            html += '<div class="th-popup-row"><i class="fas fa-key"></i><span>ACC: <strong style="color:' + accColor + ';">' + this._esc(accText) + '</strong></span></div>';
        }

        html += '</div></div>';
        return html;
    },

    _getTimeDiff: function (dateTimeStr) {
        var now = new Date();
        var dt = new Date(dateTimeStr);
        var diffMs = now - dt;
        var mins = Math.floor(diffMs / 60000);
        if (mins < 1) return this.labels.justNow;
        if (mins < 60) return this.labels.minutesAgo.replace('{0}', mins);
        var hrs = Math.floor(mins / 60);
        if (hrs < 24) return this.labels.hoursAgo.replace('{0}', hrs);
        var days = Math.floor(hrs / 24);
        return this.labels.daysAgo.replace('{0}', days);
    },

    _esc: function (str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }
};
