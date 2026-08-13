let map;
let geocoder;
let callback;
let idleListener;
let originMarker;
let mapElement;
let userMapInteraction = false;
const markers = new Map();
let mapsLoadPromise;

function loadMaps(apiKey) {
    if (globalThis.google?.maps) {
        return Promise.resolve();
    }

    if (!apiKey) {
        return Promise.reject(new Error("Google Maps API key is not configured."));
    }

    mapsLoadPromise ??= new Promise((resolve, reject) => {
        const callbackName = `smartParkingMapsReady_${Date.now()}`;
        globalThis[callbackName] = () => {
            delete globalThis[callbackName];
            resolve();
        };

        const script = document.createElement("script");
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&callback=${callbackName}&v=weekly`;
        script.async = true;
        script.onerror = () => {
            delete globalThis[callbackName];
            reject(new Error("Google Maps could not be loaded."));
        };
        document.head.appendChild(script);
    });

    return mapsLoadPromise;
}

function markUserMapInteraction() {
    userMapInteraction = true;
}

function destinationMarkerIcon() {
    const svg = `
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="56" viewBox="0 0 40 56">
            <defs>
                <filter id="shadow" x="-20%" y="-20%" width="150%" height="160%">
                    <feDropShadow dx="0" dy="2" stdDeviation="2" flood-color="#17251f" flood-opacity=".35"/>
                </filter>
            </defs>
            <g filter="url(#shadow)">
                <path d="M20 2C10 2 2 10 2 20c0 14 18 34 18 34s18-20 18-34C38 10 30 2 20 2Z"
                      fill="#036ac4" stroke="#fff" stroke-width="3"/>
                <circle cx="20" cy="20" r="7" fill="#fff"/>
            </g>
        </svg>`;

    return {
        url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`,
        scaledSize: new google.maps.Size(40, 56),
        anchor: new google.maps.Point(20, 54)
    };
}

export async function initialize(element, apiKey, dotNetCallback) {
    callback = dotNetCallback;
    mapElement = element;
    try {
        await loadMaps(apiKey);
        map = new google.maps.Map(element, {
            center: { lat: 1.3521, lng: 103.8198 },
            zoom: 12,
            clickableIcons: false,
            fullscreenControl: false,
            mapTypeControl: false,
            streetViewControl: false,
            gestureHandling: "cooperative"
        });
        geocoder = new google.maps.Geocoder();
        mapElement.addEventListener("pointerdown", markUserMapInteraction);
        mapElement.addEventListener("wheel", markUserMapInteraction, { passive: true });
        mapElement.addEventListener("keydown", markUserMapInteraction);
        let initialIdle = true;
        idleListener = map.addListener("idle", () => {
            if (initialIdle) {
                initialIdle = false;
                return;
            }
            if (!userMapInteraction) {
                return;
            }

            userMapInteraction = false;
            const center = map.getCenter();
            callback?.invokeMethodAsync("OnMapViewportChanged", center.lat(), center.lng());
        });
        return true;
    } catch {
        return false;
    }
}

export async function geocode(searchText) {
    if (!geocoder) {
        return {
            status: "failed",
            coordinate: null,
            resolvedLabel: null,
            message: "The map provider is unavailable."
        };
    }

    try {
        const placeCandidates = await searchPlaces(searchText);
        if (placeCandidates.length === 1) {
            const candidate = placeCandidates[0];
            return {
                status: "resolved",
                coordinate: candidate.coordinate,
                resolvedLabel: candidate.address || candidate.name,
                message: null
            };
        }
        if (placeCandidates.length > 1) {
            return {
                status: "ambiguous",
                coordinate: null,
                resolvedLabel: null,
                message: null,
                candidates: placeCandidates
            };
        }

        const response = await geocoder.geocode({
            address: searchText,
            componentRestrictions: { country: "SG" }
        });
        if (!response.results?.length) {
            return {
                status: "noMatch",
                coordinate: null,
                resolvedLabel: null,
                message: "No Singapore destination matched. Add a street or postal code and try again."
            };
        }
        if (response.results[0].partial_match) {
            return {
                status: "ambiguous",
                coordinate: null,
                resolvedLabel: null,
                message: null,
                candidates: geocodeCandidates(response.results)
            };
        }

        if (response.results.length > 1) {
            return {
                status: "ambiguous",
                coordinate: null,
                resolvedLabel: null,
                message: null,
                candidates: geocodeCandidates(response.results)
            };
        }

        const result = response.results[0];
        return {
            status: "resolved",
            coordinate: {
                latitude: result.geometry.location.lat(),
                longitude: result.geometry.location.lng()
            },
            resolvedLabel: result.formatted_address,
            message: null
        };
    } catch {
        return {
            status: "failed",
            coordinate: null,
            resolvedLabel: null,
            message: "Destination search failed. Please try again."
        };
    }
}

async function searchPlaces(searchText) {
    const { Place } = await google.maps.importLibrary("places");
    const { places } = await Place.searchByText({
        textQuery: searchText,
        fields: ["displayName", "formattedAddress", "location"],
        locationRestriction: {
            south: 1.13,
            west: 103.59,
            north: 1.48,
            east: 104.10
        },
        language: "en",
        region: "sg",
        maxResultCount: 5
    });
    const seen = new Set();
    return places
        .filter(place => place.displayName && place.location)
        .filter(place => {
            const key = `${place.displayName}|${place.formattedAddress ?? ""}`;
            if (seen.has(key)) {
                return false;
            }

            seen.add(key);
            return true;
        })
        .map(place => ({
            name: place.displayName,
            address: place.formattedAddress ?? null,
            coordinate: {
                latitude: place.location.lat(),
                longitude: place.location.lng()
            }
        }));
}

function geocodeCandidates(results) {
    const seen = new Set();
    return results
        .filter(result => result.formatted_address && result.geometry?.location)
        .filter(result => {
            if (seen.has(result.formatted_address)) {
                return false;
            }

            seen.add(result.formatted_address);
            return true;
        })
        .slice(0, 5)
        .map(result => ({
            name: result.formatted_address,
            address: null,
            coordinate: {
                latitude: result.geometry.location.lat(),
                longitude: result.geometry.location.lng()
            }
        }));
}

export function requestCurrentLocation() {
    return new Promise(resolve => {
        if (!navigator.geolocation) {
            resolve({
                status: "unavailable",
                coordinate: null,
                message: "Location is not supported by this browser."
            });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            position => resolve({
                status: "granted",
                coordinate: {
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude
                },
                message: "Using your current location for this circuit only."
            }),
            error => resolve({
                status: error.code === error.PERMISSION_DENIED ? "denied" : "failed",
                coordinate: null,
                message: error.code === error.PERMISSION_DENIED
                    ? "Location permission was denied."
                    : "Your location could not be determined."
            }),
            { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 });
    });
}

export async function resolveLocation(coordinate) {
    if (!geocoder || !coordinate) {
        return null;
    }

    const { Place, SearchNearbyRankPreference } = await google.maps.importLibrary("places");
    const { places } = await Place.searchNearby({
        fields: ["displayName", "formattedAddress", "location"],
        locationRestriction: {
            center: {
                lat: coordinate.latitude,
                lng: coordinate.longitude
            },
            radius: 75
        },
        maxResultCount: 1,
        rankPreference: SearchNearbyRankPreference.DISTANCE
    });
    const nearestPlace = places[0];
    if (nearestPlace?.displayName) {
        return {
            name: nearestPlace.displayName,
            address: nearestPlace.formattedAddress ?? null
        };
    }

    const response = await geocoder.geocode({
        location: {
            lat: coordinate.latitude,
            lng: coordinate.longitude
        }
    });
    const address = response.results?.[0]?.formatted_address;
    return address
        ? { name: address, address }
        : null;
}

export function setMarkers(items, origin, originLabel) {
    if (!map || !globalThis.google?.maps) {
        return;
    }

    const nextIds = new Set(items.map(item => item.carParkNumber));
    for (const [id, marker] of markers) {
        if (!nextIds.has(id)) {
            marker.setMap(null);
            markers.delete(id);
        }
    }

    const bounds = new google.maps.LatLngBounds();
    for (const item of items) {
        let marker = markers.get(item.carParkNumber);
        const position = { lat: item.latitude, lng: item.longitude };
        if (!marker) {
            marker = new google.maps.Marker({
                map,
                position,
                title: `${item.label}. ${item.availableLots ?? "Unknown"} lots available`,
                label: item.recommended ? "★" : undefined
            });
            marker.addListener("click", () => {
                selectMarker(item.carParkNumber);
                callback?.invokeMethodAsync("OnMarkerSelected", item.carParkNumber);
            });
            markers.set(item.carParkNumber, marker);
        } else {
            marker.setPosition(position);
            marker.setTitle(`${item.label}. ${item.availableLots ?? "Unknown"} lots available`);
        }
        marker.setZIndex(item.selected ? 1000 : item.recommended ? 500 : undefined);
        marker.setAnimation(item.selected ? google.maps.Animation.BOUNCE : null);
        bounds.extend(position);
    }

    if (origin) {
        const position = { lat: origin.latitude, lng: origin.longitude };
        originMarker?.setMap(null);
        originMarker = new google.maps.Marker({
            map,
            position,
            title: originLabel || "Search destination",
            icon: destinationMarkerIcon(),
            optimized: false,
            zIndex: 2000
        });
        bounds.extend(position);
    }

    if (!bounds.isEmpty()) {
        userMapInteraction = false;
        map.fitBounds(bounds, 56);
        const listener = google.maps.event.addListenerOnce(map, "idle", () => {
            if (map.getZoom() > 17) {
                map.setZoom(17);
            }
            google.maps.event.removeListener(listener);
        });
    }
}

export function selectMarker(carParkNumber) {
    for (const [id, marker] of markers) {
        marker.setZIndex(id === carParkNumber ? 1000 : undefined);
        marker.setAnimation(id === carParkNumber ? google.maps.Animation.BOUNCE : null);
    }

    const selected = markers.get(carParkNumber);
    if (selected && map) {
        userMapInteraction = false;
        map.panTo(selected.getPosition());
    }
}

export function dispose() {
    idleListener?.remove();
    idleListener = null;
    for (const marker of markers.values()) {
        marker.setMap(null);
    }
    markers.clear();
    originMarker?.setMap(null);
    originMarker = null;
    map = null;
    geocoder = null;
    callback = null;
    userMapInteraction = false;
    mapElement?.removeEventListener("pointerdown", markUserMapInteraction);
    mapElement?.removeEventListener("wheel", markUserMapInteraction);
    mapElement?.removeEventListener("keydown", markUserMapInteraction);
    mapElement = null;
}
