"use strict";
(() => {
  var __create = Object.create;
  var __defProp = Object.defineProperty;
  var __defProps = Object.defineProperties;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropDescs = Object.getOwnPropertyDescriptors;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __getOwnPropSymbols = Object.getOwnPropertySymbols;
  var __getProtoOf = Object.getPrototypeOf;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __propIsEnum = Object.prototype.propertyIsEnumerable;
  var __defNormalProp = (obj, key, value2) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value: value2 }) : obj[key] = value2;
  var __spreadValues = (a, b) => {
    for (var prop in b || (b = {}))
      if (__hasOwnProp.call(b, prop))
        __defNormalProp(a, prop, b[prop]);
    if (__getOwnPropSymbols)
      for (var prop of __getOwnPropSymbols(b)) {
        if (__propIsEnum.call(b, prop))
          __defNormalProp(a, prop, b[prop]);
      }
    return a;
  };
  var __spreadProps = (a, b) => __defProps(a, __getOwnPropDescs(b));
  var __name = (target, value2) => __defProp(target, "name", { value: value2, configurable: true });
  var __commonJS = (cb, mod) => function __require() {
    return mod || (0, cb[__getOwnPropNames(cb)[0]])((mod = { exports: {} }).exports, mod), mod.exports;
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
    // If the importer is in node compatibility mode or this is not an ESM
    // file that has been converted to a CommonJS file using a Babel-
    // compatible transform (i.e. "__esModule" has not been set), then set
    // "default" to the CommonJS "module.exports" for node compatibility.
    isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
    mod
  ));

  // global-externals:@microsoft/msfs-sdk
  var require_msfs_sdk = __commonJS({
    "global-externals:@microsoft/msfs-sdk"(exports, module) {
      module.exports = msfssdk;
    }
  });

  // ../../../third_party/msfs_efb_sample/PackageSources/efb_api/dist/index.js
  var import_msfs_sdk = __toESM(require_msfs_sdk(), 1);
  var __defProp2 = Object.defineProperty;
  var __defProps2 = Object.defineProperties;
  var __getOwnPropDescs2 = Object.getOwnPropertyDescriptors;
  var __getOwnPropSymbols2 = Object.getOwnPropertySymbols;
  var __hasOwnProp2 = Object.prototype.hasOwnProperty;
  var __propIsEnum2 = Object.prototype.propertyIsEnumerable;
  var __defNormalProp2 = /* @__PURE__ */ __name((obj, key, value2) => key in obj ? __defProp2(obj, key, { enumerable: true, configurable: true, writable: true, value: value2 }) : obj[key] = value2, "__defNormalProp");
  var __spreadValues2 = /* @__PURE__ */ __name((a, b) => {
    for (var prop in b || (b = {}))
      if (__hasOwnProp2.call(b, prop))
        __defNormalProp2(a, prop, b[prop]);
    if (__getOwnPropSymbols2)
      for (var prop of __getOwnPropSymbols2(b)) {
        if (__propIsEnum2.call(b, prop))
          __defNormalProp2(a, prop, b[prop]);
      }
    return a;
  }, "__spreadValues");
  var __spreadProps2 = /* @__PURE__ */ __name((a, b) => __defProps2(a, __getOwnPropDescs2(b)), "__spreadProps");
  var __objRest = /* @__PURE__ */ __name((source, exclude) => {
    var target = {};
    for (var prop in source)
      if (__hasOwnProp2.call(source, prop) && exclude.indexOf(prop) < 0)
        target[prop] = source[prop];
    if (source != null && __getOwnPropSymbols2)
      for (var prop of __getOwnPropSymbols2(source)) {
        if (exclude.indexOf(prop) < 0 && __propIsEnum2.call(source, prop))
          target[prop] = source[prop];
      }
    return target;
  }, "__objRest");
  var AppBootMode = /* @__PURE__ */ ((AppBootMode2) => {
    AppBootMode2[AppBootMode2["COLD"] = 0] = "COLD";
    AppBootMode2[AppBootMode2["WARM"] = 1] = "WARM";
    AppBootMode2[AppBootMode2["HOT"] = 2] = "HOT";
    return AppBootMode2;
  })(AppBootMode || {});
  var AppSuspendMode = /* @__PURE__ */ ((AppSuspendMode2) => {
    AppSuspendMode2[AppSuspendMode2["SLEEP"] = 0] = "SLEEP";
    AppSuspendMode2[AppSuspendMode2["TERMINATE"] = 1] = "TERMINATE";
    return AppSuspendMode2;
  })(AppSuspendMode || {});
  var uid = 0;
  var _Container = class _Container {
    // eslint-disable-next-line @typescript-eslint/no-empty-function
    constructor() {
      this._uid = uid++;
      this._registeredAppsPromises = [];
      this._installedApps = import_msfs_sdk.ArraySubject.create();
    }
    /**
     * Static singleton instance of Efb container
     * @internal
     */
    static get instance() {
      return window.EFB_API = _Container._instance = window.EFB_API || _Container._instance || new _Container();
    }
    /** @internal */
    apps() {
      return this._installedApps;
    }
    /** @internal */
    allAppsLoaded() {
      return this._registeredAppsPromises.length === this._installedApps.length;
    }
    /**
     * Method used by the OS to share the bus to apps
     * @internal
     */
    setBus(bus) {
      this.bus = bus;
      return this;
    }
    /**
     * Method used by the OS to share the units settings manager to the apps
     * @internal
     */
    setUnitsSettingManager(unitsSettingManager) {
      this.unitsSettingManager = unitsSettingManager;
      return this;
    }
    /**
     * Method used by the OS to share the settings manager to the apps
     * @internal
     */
    setEfbSettingManager(efbSettingsManager) {
      this.efbSettingsManager = efbSettingsManager;
      return this;
    }
    setOnboardingManager(onboardingManager) {
      this.onboardingManager = onboardingManager;
      return this;
    }
    /**
     * Method used by the OS to share the notification manager to the apps
     * @internal
     */
    setNotificationManager(notificationManager) {
      this.notificationManager = notificationManager;
      return this;
    }
    /**
     * Load stylesheet
     * @param uri
     * @returns Promise which is resolved when stylesheet is loaded or rejected if an error occur.
     */
    async loadCss(uri) {
      if (document.querySelector(`link[href*="${uri}"]`)) {
        return Promise.reject(`${uri} already loaded.`);
      }
      const linkTag = document.createElement("link");
      linkTag.rel = "stylesheet";
      linkTag.href = uri;
      document.head.append(linkTag);
      return new Promise((resolve, reject) => {
        linkTag.onload = () => resolve();
        linkTag.onerror = reject;
      });
    }
    /**
     * Load script file
     * @param uri
     * @returns Promise which is resolved when script is loaded or rejected if an error occur.
     */
    async loadJs(uri) {
      if (document.querySelector(`script[src*="${uri}"]`)) {
        return Promise.reject(`${uri} already loaded.`);
      }
      const scriptTag = document.createElement("script");
      scriptTag.type = "text/javascript";
      scriptTag.src = uri;
      document.head.append(scriptTag);
      return new Promise((resolve, reject) => {
        scriptTag.onload = () => resolve();
        scriptTag.onerror = reject;
      });
    }
    /**
     * Register an app in EFB
     * @template T - App registration options
     * @param app The app you wan't to register
     * @param options Options you'r app might need when installing
     * @returns EFB instance
     * @throws Throw an error if App install went wrong.
     */
    use(app, ...options) {
      var _a13;
      try {
        if (!this.bus) {
          throw new Error(`Bus has not been initialized yet.`);
        }
        const appInstance = app instanceof App ? app : new app();
        const installProps = {
          bus: this.bus,
          unitsSettingManager: this.unitsSettingManager,
          efbSettingsManager: this.efbSettingsManager,
          notificationManager: this.notificationManager,
          onboardingManager: this.onboardingManager,
          options: Object.keys(options).filter((key) => key !== "isCoreApp").reduce((acc, key) => {
            return Object.assign(acc, options[key]);
          }, {})
        };
        const appInstaller = appInstance._install.apply(appInstance, [installProps]);
        const name = appInstance.internalName;
        if (/\s/.test(name)) {
          throw new Error(`The App name can't have any whitespace character. "${name}"`);
        }
        this._registeredAppsPromises.push(
          appInstaller.then(() => {
            this._installedApps.insert(appInstance);
          })
        );
      } catch (e) {
        (_a13 = document.currentScript) == null ? void 0 : _a13.remove();
        console.error(`App can't be installed`, e);
        throw e;
      }
      return this;
    }
  };
  __name(_Container, "Container");
  var Container = _Container;
  var Efb = Container.instance;
  var EfbApiVersion = "1.0.3";
  var _App = class _App {
    constructor() {
      this._isInstalled = false;
      this._isReady = false;
      this._favoriteIndex = -1;
      this.available = import_msfs_sdk.Subject.create(true);
      this.BootMode = AppBootMode.COLD;
      this.SuspendMode = AppSuspendMode.SLEEP;
    }
    /**
     * @param props
     * @internal
     */
    async _install(props) {
      var _a13;
      if (this._isInstalled) {
        return Promise.reject("App already installed.");
      }
      this._isInstalled = true;
      this.bus = props.bus;
      this._unitsSettingsManager = props.unitsSettingManager;
      this._efbSettingsManager = props.efbSettingsManager;
      this._notificationManager = props.notificationManager;
      this._onboardingManager = props.onboardingManager;
      this._favoriteIndex = (_a13 = props.favoriteIndex) != null ? _a13 : -1;
      this.options = props.options;
      await this.install(props);
      this._isReady = true;
      Coherent.trigger("EFB_APP_INSTALLED", this.name, this.internalName, EfbApiVersion, this.getVersion());
      return Promise.resolve();
    }
    /**
     * Install hook
     * @param props
     */
    async install(props) {
      return Promise.resolve();
    }
    /** Boolean to check if app is loaded and installed. */
    get isReady() {
      return this._isReady;
    }
    /**
     * Internal app name
     * @defaultValue > Class's name (`this.constructor.name`)
     */
    get internalName() {
      return this.constructor.name;
    }
    /**
     * EFB units settings manager
     * @returns a unique unitsSettingsManager instance
     */
    get unitsSettingsManager() {
      const unitsSettingsManager = this._unitsSettingsManager;
      if (!unitsSettingsManager) {
        throw new Error("Units settings manager is not defined");
      }
      return unitsSettingsManager;
    }
    /**
     * EFB settings manager
     * @returns a unique efbSettingsManager instance
     */
    get efbSettingsManager() {
      const efbSettingsManager = this._efbSettingsManager;
      if (!efbSettingsManager) {
        throw new Error("EFB settings manager is not defined");
      }
      return efbSettingsManager;
    }
    /**
     * EFB notification manager
     * @returns a unique efbNotificationManager instance
     */
    get notificationManager() {
      const notificationManager = this._notificationManager;
      if (!notificationManager) {
        throw new Error("Notification manager is not defined");
      }
      return notificationManager;
    }
    /** Onboarding manager */
    get onboardingManager() {
      const onboardingManager = this._onboardingManager;
      if (!onboardingManager) {
        throw new Error("Onboarding manager is not defined");
      }
      return onboardingManager;
    }
    /**
     * Aircraft models list compatible with the App. If not defined, the App is compatible with all aircraft models.
     * example: ['Cabri G2', 'H125']
     * @returns a list of aircraft models compatible with the App or undefined
     */
    get compatibleAircraftModels() {
      return void 0;
    }
    /** @internal */
    get favoriteIndex() {
      return this._favoriteIndex;
    }
    /** @internal */
    set favoriteIndex(index2) {
      this._favoriteIndex = index2;
    }
    /**
     * @since 1.0.3
     * @returns True if the app is searchable (ie: app list)
     */
    getIsSearchable() {
      var _a13;
      return (_a13 = this.options.isSearchable) != null ? _a13 : true;
    }
    /**
     * @since 1.0.3
     * @returns True if the app can be added to favorites
     */
    getIsFavoritable() {
      var _a13;
      return (_a13 = this.options.isFavoritable) != null ? _a13 : true;
    }
    /**
     * @since 1.0.3
     * @returns Return app version
     */
    getVersion() {
      return "";
    }
  };
  __name(_App, "App");
  var App = _App;
  function isVNode(object) {
    return "instance" in object && "props" in object && "children" in object;
  }
  __name(isVNode, "isVNode");
  function isFunction(fn) {
    return typeof fn === "function";
  }
  __name(isFunction, "isFunction");
  function toClassProp(classProp) {
    if (classProp === void 0) {
      return classProp;
    }
    if (Array.isArray(classProp)) {
      return toClassProp(classProp.join(" "));
    }
    if (typeof classProp !== "string") {
      return classProp;
    }
    return classProp.split(" ").reduce(function(stack, el) {
      return __spreadProps2(__spreadValues2({}, stack), { [el]: true });
    }, {});
  }
  __name(toClassProp, "toClassProp");
  function mergeClassProp(baseProp, ...args) {
    const mergedClassProp = Object.assign({}, toClassProp(baseProp));
    for (const arg of args) {
      Object.assign(mergedClassProp, toClassProp(arg));
    }
    return mergedClassProp;
  }
  __name(mergeClassProp, "mergeClassProp");
  function where(value2) {
    return (input) => value2 === input;
  }
  __name(where, "where");
  function toString() {
    return (input) => input.toString();
  }
  __name(toString, "toString");
  var __FlightPhaseManager = class __FlightPhaseManager {
    // eslint-disable-next-line @typescript-eslint/no-empty-function
    constructor() {
      this._flightPhase = import_msfs_sdk.Subject.create(
        100
        /* UNKNOWN */
      );
      this.flightPhase = this._flightPhase;
      this.isFlightOver = this._flightPhase.map(where(
        11
        /* FLIGHT_OVER */
      ));
      this.onFlightPhaseStateChangedSubscription = null;
    }
    /**
     * Static singleton instance of the Flight Phase Manager
     * @internal
     */
    static get instance() {
      return window.FLIGHT_PHASE_MANAGER = __FlightPhaseManager._instance = window.FLIGHT_PHASE_MANAGER || __FlightPhaseManager._instance || new __FlightPhaseManager();
    }
    /**
     * The bus is set once at EFB initialization from efb_ui.tsx
     * @internal
     */
    setBus(bus) {
      var _a13;
      (_a13 = this.onFlightPhaseStateChangedSubscription) == null ? void 0 : _a13.destroy();
      this.onFlightPhaseStateChangedSubscription = bus.on("FlightPhaseChanged", (flightPhase) => {
        switch (flightPhase) {
          case "PREFLIGHT_RTC":
          case "PREFLIGHT":
          case "SKIP_TRANSITION_PREFLIGHT":
            this._flightPhase.set(
              0
              /* PREFLIGHT */
            );
            break;
          case "STARTUP":
            this._flightPhase.set(
              1
              /* STARTUP */
            );
            break;
          case "BEFORE_TAXI":
          case "TAXI":
          case "SKIP_TRANSITION_TAXI":
            this._flightPhase.set(
              3
              /* TAXI */
            );
            break;
          case "TAKEOFF":
            this._flightPhase.set(
              4
              /* TAKEOFF */
            );
            break;
          case "CLIMB":
            this._flightPhase.set(
              5
              /* CLIMB */
            );
            break;
          case "CRUISE":
          case "SKIP_TRANSITION_CRUISE":
            this._flightPhase.set(
              6
              /* CRUISE */
            );
            break;
          case "DESCENT":
            this._flightPhase.set(
              7
              /* DESCENT */
            );
            break;
          case "LANDING":
            this._flightPhase.set(
              8
              /* LANDING */
            );
            break;
          case "TAXITOGATE":
          case "SKIP_TRANSITION_TAXITOGATE":
            this._flightPhase.set(
              9
              /* TAXITOGATE */
            );
            break;
          case "SHUTDOWN":
            this._flightPhase.set(
              10
              /* SHUTDOWN */
            );
            break;
          case "RTC":
            break;
          case "MISSIONSUCCESS":
          case "MISSIONABORTED":
            this._flightPhase.set(
              11
              /* FLIGHT_OVER */
            );
            break;
          case "REACH BANNER APPROACH":
          case "HOOKBANNER":
          case "REACH PASS":
          case "PASS":
          case "CRUISEBACK":
          case "REACH DROP":
          case "DROP BANNER":
            break;
          case "WATERDROP":
            break;
          default:
            console.warn(`Received unknown flight phase '${flightPhase}'`);
            this._flightPhase.set(
              100
              /* UNKNOWN */
            );
        }
      });
    }
    /**
     * @description This function is used in order to verify if a given flight phase has been reached.
     * Always return true when the flight phase is unknown.
     * @param flightPhase The flight phase that has been reached or not
     * @returns A subscribable that returns whether the given flight phase has been reached
     */
    hasReachedFlightPhaseState(flightPhase) {
      return this.flightPhase.map(
        (currentFlightPhaseState) => currentFlightPhaseState >= flightPhase
      );
    }
  };
  __name(__FlightPhaseManager, "_FlightPhaseManager");
  var _FlightPhaseManager = __FlightPhaseManager;
  var FlightPhaseManager = _FlightPhaseManager.instance;
  var __GameModeManager = class __GameModeManager {
    // eslint-disable-next-line @typescript-eslint/no-empty-function
    constructor() {
      this._gameMode = import_msfs_sdk.Subject.create(
        0
        /* UNKNOWN */
      );
      this.gameMode = this._gameMode;
      this.isCareer = this._gameMode.map(where(
        1
        /* CAREER */
      ));
      this.isChallenge = this._gameMode.map(where(
        2
        /* CHALLENGE */
      ));
      this.isDiscovery = this._gameMode.map(where(
        3
        /* DISCOVERY */
      ));
      this.isFreeflight = this._gameMode.map(where(
        4
        /* FREEFLIGHT */
      ));
      this.isInMenu = import_msfs_sdk.Subject.create(true);
      this.onGameModeChangedSubscription = null;
      this.onIsInMenuSubscription = null;
    }
    /**
     * Static singleton instance of the Game mode manager
     * @internal
     */
    static get instance() {
      return window.GAME_MODE_MANAGER = __GameModeManager._instance = window.GAME_MODE_MANAGER || __GameModeManager._instance || new __GameModeManager();
    }
    /**
     * The bus is set once at EFB initialization from efb_ui.tsx
     * @internal
     */
    setBus(bus) {
      var _a13, _b;
      (_a13 = this.onGameModeChangedSubscription) == null ? void 0 : _a13.destroy();
      (_b = this.onIsInMenuSubscription) == null ? void 0 : _b.destroy();
      this.onGameModeChangedSubscription = bus.on("GameModeChanged", (gameMode) => {
        switch (gameMode) {
          case "":
            this._gameMode.set(
              0
              /* UNKNOWN */
            );
            break;
          case "CAREER GAMEMODE":
            this._gameMode.set(
              1
              /* CAREER */
            );
            break;
          case "CHALLENGE GAMEMODE":
            this._gameMode.set(
              2
              /* CHALLENGE */
            );
            break;
          case "DISCOVERY GAMEMODE":
            this._gameMode.set(
              3
              /* DISCOVERY */
            );
            break;
          case "FREEFLIGHT GAMEMODE":
            this._gameMode.set(
              4
              /* FREEFLIGHT */
            );
            break;
          default:
            console.error(`Unknown game mode '${gameMode}'`);
            this._gameMode.set(
              0
              /* UNKNOWN */
            );
        }
      });
      this.onIsInMenuSubscription = bus.on("IsInMenuUpdate", (isInMenu) => this.isInMenu.set(isInMenu));
    }
  };
  __name(__GameModeManager, "_GameModeManager");
  var _GameModeManager = __GameModeManager;
  var GameModeManager = _GameModeManager.instance;
  Blob.prototype.arrayBuffer = function() {
    return new Promise((resolve, reject) => {
      const fileReader = new FileReader();
      fileReader.onload = function(event) {
        var _a13;
        const arrayBuffer = (_a13 = event.target) == null ? void 0 : _a13.result;
        if (typeof arrayBuffer === "string" || arrayBuffer === null || arrayBuffer === void 0) {
          reject("ArrayBuffer is null");
          return;
        }
        resolve(arrayBuffer);
      };
      fileReader.readAsArrayBuffer(this);
      fileReader.result;
    });
  };
  var uint8 = new Uint8Array(4);
  var uint32 = new Uint32Array(uint8.buffer);
  function isPromise(value2) {
    return value2 instanceof Promise;
  }
  __name(isPromise, "isPromise");
  function toPromise(value2) {
    return isPromise(value2) ? value2 : Promise.resolve(value2);
  }
  __name(toPromise, "toPromise");
  function checkUserSetting(setting, type) {
    if (!Object.values(type).includes(setting.get())) {
      setting.resetToDefault();
    }
  }
  __name(checkUserSetting, "checkUserSetting");
  var basicFormatter = import_msfs_sdk.NumberFormatter.create({
    maxDigits: 0,
    forceDecimalZeroes: false,
    nanString: "-"
  });
  var _a;
  var _UnitFormatter = (_a = class {
    /**
     * Creates a function which formats measurement units to strings representing their abbreviated names.
     * @param defaultString The string to output when the input unit cannot be formatted. Defaults to the empty string.
     * @param charCase The case to enforce on the output string. Defaults to `'normal'`.
     * @returns A function which formats measurement units to strings representing their abbreviated names.
     */
    static create(defaultString = "", charCase = "normal") {
      var _a13, _b;
      switch (charCase) {
        case "upper":
          (_a13 = _a.UNIT_TEXT_UPPER) != null ? _a13 : _a.UNIT_TEXT_UPPER = _a.createUpperCase();
          return (unit) => {
            var _a22, _b2;
            return (
              // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
              (_b2 = (_a22 = _a.UNIT_TEXT_UPPER[unit.family]) == null ? void 0 : _a22[unit.name]) != null ? _b2 : defaultString
            );
          };
        case "lower":
          (_b = _a.UNIT_TEXT_LOWER) != null ? _b : _a.UNIT_TEXT_LOWER = _a.createLowerCase();
          return (unit) => {
            var _a22, _b2;
            return (
              // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
              (_b2 = (_a22 = _a.UNIT_TEXT_LOWER[unit.family]) == null ? void 0 : _a22[unit.name]) != null ? _b2 : defaultString
            );
          };
        default:
          return (unit) => {
            var _a22, _b2;
            return (_b2 = (_a22 = _a.UNIT_TEXT[unit.family]) == null ? void 0 : _a22[unit.name]) != null ? _b2 : defaultString;
          };
      }
    }
    /**
     * Creates a record of lowercase unit abbreviated names.
     * @returns A record of lowercase unit abbreviated names.
     */
    static createLowerCase() {
      const lower = {};
      for (const family in _a.UNIT_TEXT) {
        const familyText = _a.UNIT_TEXT[family];
        lower[family] = {};
        for (const unit in familyText) {
          lower[family][unit] = familyText[unit].toLowerCase();
        }
      }
      return lower;
    }
    /**
     * Creates a record of uppercase unit abbreviated names.
     * @returns A record of uppercase unit abbreviated names.
     */
    static createUpperCase() {
      const upper = {};
      for (const family in _a.UNIT_TEXT) {
        const familyText = _a.UNIT_TEXT[family];
        upper[family] = {};
        for (const unit in familyText) {
          upper[family][unit] = familyText[unit].toUpperCase();
        }
      }
      return upper;
    }
    /**
     * Gets a mapping of unit family and name to text used by UnitFormatter to format units. The returned object maps
     * unit families to objects that map unit names within each family to formatted text.
     * @returns A mapping of unit family and name to text used by UnitFormatter to format units.
     */
    static getUnitTextMap() {
      return _a.UNIT_TEXT;
    }
  }, __name(_a, "_UnitFormatter"), _a);
  _UnitFormatter.UNIT_TEXT = {
    [import_msfs_sdk.UnitFamily.Distance]: {
      [import_msfs_sdk.UnitType.METER.name]: "M",
      [import_msfs_sdk.UnitType.FOOT.name]: "FT",
      [import_msfs_sdk.UnitType.KILOMETER.name]: "KM",
      [import_msfs_sdk.UnitType.NMILE.name]: "NM",
      [import_msfs_sdk.UnitType.MILE.name]: "SM"
    },
    [import_msfs_sdk.UnitFamily.Angle]: {
      [import_msfs_sdk.UnitType.DEGREE.name]: "\xB0",
      [import_msfs_sdk.UnitType.RADIAN.name]: "rad"
    },
    [import_msfs_sdk.UnitFamily.Duration]: {
      [import_msfs_sdk.UnitType.SECOND.name]: "SEC",
      [import_msfs_sdk.UnitType.MINUTE.name]: "MIN",
      [import_msfs_sdk.UnitType.HOUR.name]: "HR"
    },
    [import_msfs_sdk.UnitFamily.Weight]: {
      [import_msfs_sdk.UnitType.KILOGRAM.name]: "KG",
      [import_msfs_sdk.UnitType.POUND.name]: "LBS",
      [import_msfs_sdk.UnitType.LITER_FUEL.name]: "LT",
      [import_msfs_sdk.UnitType.GALLON_FUEL.name]: "GAL",
      [import_msfs_sdk.UnitType.IMP_GALLON_FUEL.name]: "IG"
    },
    [import_msfs_sdk.UnitFamily.Volume]: {
      [import_msfs_sdk.UnitType.LITER.name]: "L",
      [import_msfs_sdk.UnitType.GALLON.name]: "GAL"
    },
    [import_msfs_sdk.UnitFamily.Pressure]: {
      [import_msfs_sdk.UnitType.HPA.name]: "HPA",
      [import_msfs_sdk.UnitType.IN_HG.name]: "IN"
    },
    [import_msfs_sdk.UnitFamily.Temperature]: {
      [import_msfs_sdk.UnitType.CELSIUS.name]: "\xB0C",
      [import_msfs_sdk.UnitType.FAHRENHEIT.name]: "\xB0F"
    },
    [import_msfs_sdk.UnitFamily.TemperatureDelta]: {
      [import_msfs_sdk.UnitType.DELTA_CELSIUS.name]: "\xB0C",
      [import_msfs_sdk.UnitType.DELTA_FAHRENHEIT.name]: "\xB0F"
    },
    [import_msfs_sdk.UnitFamily.Speed]: {
      [import_msfs_sdk.UnitType.KNOT.name]: "KT",
      [import_msfs_sdk.UnitType.KPH.name]: "KM/H",
      [import_msfs_sdk.UnitType.MPM.name]: "MPM",
      [import_msfs_sdk.UnitType.FPM.name]: "FPM"
    },
    [import_msfs_sdk.UnitFamily.WeightFlux]: {
      [import_msfs_sdk.UnitType.KGH.name]: "KG/HR",
      [import_msfs_sdk.UnitType.PPH.name]: "LB/HR",
      [import_msfs_sdk.UnitType.LPH_FUEL.name]: "LT/HR",
      [import_msfs_sdk.UnitType.GPH_FUEL.name]: "GAL/HR",
      [import_msfs_sdk.UnitType.IGPH_FUEL.name]: "IG/HR"
    }
  };
  var UnitFormatter = _UnitFormatter;
  function value(arg) {
    return typeof arg === "function" ? arg() : arg;
  }
  __name(value, "value");
  var _AppContainer = class _AppContainer extends import_msfs_sdk.DisplayComponent {
    constructor() {
      super(...arguments);
      this.appMainRef = import_msfs_sdk.FSComponent.createRef();
      this.appStackRef = import_msfs_sdk.FSComponent.createRef();
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.props.appViewService.onAppContainerRendered(this.appStackRef.instance);
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { ref: this.appMainRef, class: `app-container` }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(DefaultAppViewStackContainer, { ref: this.appStackRef }));
    }
  };
  __name(_AppContainer, "AppContainer");
  var AppContainer = _AppContainer;
  var _DefaultAppViewStackContainer = class _DefaultAppViewStackContainer extends import_msfs_sdk.DisplayComponent {
    constructor() {
      super(...arguments);
      this.rootRef = import_msfs_sdk.FSComponent.createRef();
    }
    renderView(view) {
      import_msfs_sdk.FSComponent.render(view, this.rootRef.instance);
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { ref: this.rootRef, class: mergeClassProp("app-view-stack", this.props.class) });
    }
  };
  __name(_DefaultAppViewStackContainer, "DefaultAppViewStackContainer");
  var DefaultAppViewStackContainer = _DefaultAppViewStackContainer;
  function Memoize(args) {
    let hashFunction;
    let duration;
    let tags;
    {
      hashFunction = args;
    }
    return (target, propertyKey, descriptor) => {
      if (descriptor.value != null) {
        descriptor.value = getNewFunction(descriptor.value, hashFunction, duration, tags);
      } else if (descriptor.get != null) {
        descriptor.get = getNewFunction(descriptor.get, hashFunction, duration, tags);
      } else {
        throw "Only put a Memoize() decorator on a method or get accessor.";
      }
    };
  }
  __name(Memoize, "Memoize");
  var clearCacheTagsMap = /* @__PURE__ */ new Map();
  function getNewFunction(originalMethod, hashFunction, duration = 0, tags) {
    const propMapName = Symbol(`__memoized_map__`);
    return function(...args) {
      let returnedValue;
      if (!this.hasOwnProperty(propMapName)) {
        Object.defineProperty(this, propMapName, {
          configurable: false,
          enumerable: false,
          writable: false,
          value: /* @__PURE__ */ new Map()
        });
      }
      let myMap = this[propMapName];
      if (Array.isArray(tags)) {
        for (const tag of tags) {
          if (clearCacheTagsMap.has(tag)) {
            clearCacheTagsMap.get(tag).push(myMap);
          } else {
            clearCacheTagsMap.set(tag, [myMap]);
          }
        }
      }
      if (hashFunction || args.length > 0 || duration > 0) {
        let hashKey;
        if (hashFunction === true) {
          hashKey = args.map((a) => a.toString()).join("!");
        } else if (hashFunction) {
          hashKey = hashFunction.apply(this, args);
        } else {
          hashKey = args[0];
        }
        const timestampKey = `${hashKey}__timestamp`;
        let isExpired = false;
        if (duration > 0) {
          if (!myMap.has(timestampKey)) {
            isExpired = true;
          } else {
            let timestamp = myMap.get(timestampKey);
            isExpired = Date.now() - timestamp > duration;
          }
        }
        if (myMap.has(hashKey) && !isExpired) {
          returnedValue = myMap.get(hashKey);
        } else {
          returnedValue = originalMethod.apply(this, args);
          myMap.set(hashKey, returnedValue);
          if (duration > 0) {
            myMap.set(timestampKey, Date.now());
          }
        }
      } else {
        const hashKey = this;
        if (myMap.has(hashKey)) {
          returnedValue = myMap.get(hashKey);
        } else {
          returnedValue = originalMethod.apply(this, args);
          myMap.set(hashKey, returnedValue);
        }
      }
      return returnedValue;
    };
  }
  __name(getNewFunction, "getNewFunction");
  var GamepadEvents = /* @__PURE__ */ ((GamepadEvents2) => {
    GamepadEvents2["JOYSTICK_LEFT_X_AXIS"] = "JOYSTICK_LEFT_X_AXIS";
    GamepadEvents2["JOYSTICK_LEFT_Y_AXIS"] = "JOYSTICK_LEFT_Y_AXIS";
    GamepadEvents2["JOYSTICK_RIGHT_X_AXIS"] = "JOYSTICK_RIGHT_X_AXIS";
    GamepadEvents2["JOYSTICK_RIGHT_Y_AXIS"] = "JOYSTICK_RIGH_Y_AXIS";
    GamepadEvents2["BUTTON_A"] = "BUTTON_A";
    GamepadEvents2["BUTTON_B"] = "BUTTON_B";
    GamepadEvents2["BUTTON_Y"] = "BUTTON_Y";
    GamepadEvents2["BUTTON_X"] = "BUTTON_X";
    GamepadEvents2["JOYDIR_LEFT"] = "JOYDIR_LEFT";
    GamepadEvents2["JOYDIR_RIGHT"] = "JOYDIR_RIGHT";
    GamepadEvents2["JOYDIR_UP"] = "JOYDIR_UP";
    GamepadEvents2["JOYDIR_DOWN"] = "JOYDIR_DOWN";
    return GamepadEvents2;
  })(GamepadEvents || {});
  var InputAction = /* @__PURE__ */ ((InputAction2) => {
    InputAction2[InputAction2["SECONDARY"] = 0] = "SECONDARY";
    InputAction2[InputAction2["TERTIARY"] = 1] = "TERTIARY";
    InputAction2[InputAction2["PREV"] = 2] = "PREV";
    InputAction2[InputAction2["NEXT"] = 3] = "NEXT";
    InputAction2[InputAction2["PREV_SUB"] = 4] = "PREV_SUB";
    InputAction2[InputAction2["NEXT_SUB"] = 5] = "NEXT_SUB";
    InputAction2[InputAction2["ZOOM_OUT"] = 6] = "ZOOM_OUT";
    InputAction2[InputAction2["ZOOM_IN"] = 7] = "ZOOM_IN";
    InputAction2[InputAction2["SCROLL_UP"] = 8] = "SCROLL_UP";
    InputAction2[InputAction2["SCROLL_RIGHT"] = 9] = "SCROLL_RIGHT";
    InputAction2[InputAction2["SCROLL_DOWN"] = 10] = "SCROLL_DOWN";
    InputAction2[InputAction2["SCROLL_LEFT"] = 11] = "SCROLL_LEFT";
    return InputAction2;
  })(InputAction || {});
  var InputActions = {
    [
      0
      /* SECONDARY */
    ]: "KEY_EFB_SECONDARY",
    [
      1
      /* TERTIARY */
    ]: "KEY_EFB_TERTIARY",
    [
      2
      /* PREV */
    ]: "KEY_EFB_NAV_TABS_PREV",
    [
      3
      /* NEXT */
    ]: "KEY_EFB_NAV_TABS_NEXT",
    [
      4
      /* PREV_SUB */
    ]: "KEY_EFB_ZOOM_OUT",
    [
      5
      /* NEXT_SUB */
    ]: "KEY_EFB_ZOOM_IN",
    [
      6
      /* ZOOM_OUT */
    ]: "KEY_EFB_ZOOM_OUT",
    [
      7
      /* ZOOM_IN */
    ]: "KEY_EFB_ZOOM_IN",
    [
      8
      /* SCROLL_UP */
    ]: "KEY_EFB_SCROLL_UP",
    [
      9
      /* SCROLL_RIGHT */
    ]: "KEY_EFB_SCROLL_RIGHT",
    [
      10
      /* SCROLL_DOWN */
    ]: "KEY_EFB_SCROLL_DOWN",
    [
      11
      /* SCROLL_LEFT */
    ]: "KEY_EFB_SCROLL_LEFT"
  };
  var InternalInputAction = /* @__PURE__ */ ((InternalInputAction2) => {
    InternalInputAction2[InternalInputAction2["VALID"] = 0] = "VALID";
    InternalInputAction2[InternalInputAction2["BACK"] = 1] = "BACK";
    InternalInputAction2[InternalInputAction2["SECONDARY"] = 2] = "SECONDARY";
    InternalInputAction2[InternalInputAction2["TERTIARY"] = 3] = "TERTIARY";
    InternalInputAction2[InternalInputAction2["PREV_TAB"] = 4] = "PREV_TAB";
    InternalInputAction2[InternalInputAction2["NEXT_TAB"] = 5] = "NEXT_TAB";
    InternalInputAction2[InternalInputAction2["ZOOM_OUT"] = 6] = "ZOOM_OUT";
    InternalInputAction2[InternalInputAction2["ZOOM_IN"] = 7] = "ZOOM_IN";
    InternalInputAction2[InternalInputAction2["UP"] = 8] = "UP";
    InternalInputAction2[InternalInputAction2["RIGHT"] = 9] = "RIGHT";
    InternalInputAction2[InternalInputAction2["DOWN"] = 10] = "DOWN";
    InternalInputAction2[InternalInputAction2["LEFT"] = 11] = "LEFT";
    InternalInputAction2[InternalInputAction2["PREV_APP"] = 12] = "PREV_APP";
    InternalInputAction2[InternalInputAction2["NEXT_APP"] = 13] = "NEXT_APP";
    InternalInputAction2[InternalInputAction2["OPEN_NOTIFICATION_MENU"] = 14] = "OPEN_NOTIFICATION_MENU";
    InternalInputAction2[InternalInputAction2["SCROLL_UP"] = 15] = "SCROLL_UP";
    InternalInputAction2[InternalInputAction2["SCROLL_RIGHT"] = 16] = "SCROLL_RIGHT";
    InternalInputAction2[InternalInputAction2["SCROLL_DOWN"] = 17] = "SCROLL_DOWN";
    InternalInputAction2[InternalInputAction2["SCROLL_LEFT"] = 18] = "SCROLL_LEFT";
    InternalInputAction2[InternalInputAction2["TEXTBOX_VALID"] = 19] = "TEXTBOX_VALID";
    InternalInputAction2[InternalInputAction2["TEXTBOX_UP"] = 20] = "TEXTBOX_UP";
    InternalInputAction2[InternalInputAction2["TEXTBOX_RIGHT"] = 21] = "TEXTBOX_RIGHT";
    InternalInputAction2[InternalInputAction2["TEXTBOX_DOWN"] = 22] = "TEXTBOX_DOWN";
    InternalInputAction2[InternalInputAction2["TEXTBOX_LEFT"] = 23] = "TEXTBOX_LEFT";
    return InternalInputAction2;
  })(InternalInputAction || {});
  var InternalInputActions = {
    [
      0
      /* VALID */
    ]: "KEY_EFB_VALID",
    [
      1
      /* BACK */
    ]: "KEY_EFB_BACK",
    [
      2
      /* SECONDARY */
    ]: "KEY_EFB_SECONDARY",
    [
      3
      /* TERTIARY */
    ]: "KEY_EFB_TERTIARY",
    [
      4
      /* PREV_TAB */
    ]: "KEY_EFB_NAV_TABS_PREV",
    [
      5
      /* NEXT_TAB */
    ]: "KEY_EFB_NAV_TABS_NEXT",
    [
      6
      /* ZOOM_OUT */
    ]: "KEY_EFB_ZOOM_OUT",
    [
      7
      /* ZOOM_IN */
    ]: "KEY_EFB_ZOOM_IN",
    [
      8
      /* UP */
    ]: "KEY_EFB_UP",
    [
      9
      /* RIGHT */
    ]: "KEY_EFB_NAV_APP_NEXT",
    [
      10
      /* DOWN */
    ]: "KEY_EFB_DOWN",
    [
      11
      /* LEFT */
    ]: "KEY_EFB_NAV_APP_PREV",
    [
      12
      /* PREV_APP */
    ]: "KEY_EFB_NAV_APP_PREV",
    [
      13
      /* NEXT_APP */
    ]: "KEY_EFB_NAV_APP_NEXT",
    [
      14
      /* OPEN_NOTIFICATION_MENU */
    ]: "KEY_EFB_NOTIFICATIONS_MENU",
    [
      15
      /* SCROLL_UP */
    ]: "KEY_EFB_SCROLL_UP",
    [
      16
      /* SCROLL_RIGHT */
    ]: "KEY_EFB_SCROLL_RIGHT",
    [
      17
      /* SCROLL_DOWN */
    ]: "KEY_EFB_SCROLL_DOWN",
    [
      18
      /* SCROLL_LEFT */
    ]: "KEY_EFB_SCROLL_LEFT",
    [
      19
      /* TEXTBOX_VALID */
    ]: "KEY_EFB_VALID_TEXT",
    [
      20
      /* TEXTBOX_UP */
    ]: "KEY_EFB_TEXTBOX_UP",
    [
      21
      /* TEXTBOX_RIGHT */
    ]: "KEY_EFB_TEXTBOX_RIGHT",
    [
      22
      /* TEXTBOX_DOWN */
    ]: "KEY_EFB_TEXTBOX_DOWN",
    [
      23
      /* TEXTBOX_LEFT */
    ]: "KEY_EFB_TEXTBOX_LEFT"
  };
  var _a2;
  var _InputsListener = (_a2 = class extends ViewListener.ViewListener {
    /**
     * @deprecated
     * @param context The input context.
     * @param action The input action.
     * @param callback The callback that will be called when the input is triggered.
     * @returns The input watcher ID.
     */
    static addInputChangeCallback(context, action, callback) {
      const idWatcher = "InputWatcher_" + context + "_" + action + "_" + import_msfs_sdk.UUID.GenerateUuid();
      _a2.inputsListener.trigger("ADD_INPUT_WATCHER", idWatcher, context, action);
      _a2.inputsListener.on("InputListener.InputChange", (id, down) => {
        if (id === idWatcher) {
          callback(down);
        }
      });
      return idWatcher;
    }
    /**
     * @deprecated
     * @param id The event ID.
     */
    static removeInputChangeCallback(id) {
      _a2.inputsListener.trigger("REMOVE_INPUT_WATCHER", id);
    }
  }, __name(_a2, "_InputsListener"), _a2);
  _InputsListener.isLoaded = import_msfs_sdk.Subject.create(false);
  _InputsListener.inputsListener = RegisterViewListener(
    "JS_LISTENER_INPUTS",
    () => _InputsListener.isLoaded.set(true)
  );
  var _InputStackListener = class _InputStackListener {
    constructor() {
      this._isReady = import_msfs_sdk.Subject.create(false);
      this.isReady = this._isReady;
      this.inputActionMap = /* @__PURE__ */ new Map();
      this.inputStackListener = RegisterViewListener(
        "JS_LISTENER_INPUT_STACK",
        () => this._isReady.set(true)
      );
      this.onInputTriggeredCallback = this._onInputTriggeredCallback.bind(this);
      this.inputStackListener.on("ON_INPUT_TRIGGERED", this.onInputTriggeredCallback);
    }
    _onInputTriggeredCallback(task, value2, _activeId, uuid) {
      var _a13;
      (_a13 = this.inputActionMap.get(uuid)) == null ? void 0 : _a13(task, value2);
    }
    /**
     * Add an action that will be executed when the given input is triggered.
     * @param input The input that, when triggered, will execute the action.
     * @param callback The action callback that will be called when the input is triggered.
     * @param inputType The trigger type of the input between 'pressed', 'released' and 'axis'. Default to 'released'.
     * @param context The context which the input will be applied to. Default to 'DEFAULT'.
     * @returns A callback that has to be called when the input action is not needed anymore.
     */
    addInputAction(input, callback, inputType = "released", context = "DEFAULT") {
      const uuid = import_msfs_sdk.UUID.GenerateUuid().toUpperCase();
      const formattedUuid = `{${uuid}}`;
      this.inputActionMap.set(uuid, (task, value2) => {
        const isConsumed = callback(value2);
        this.inputStackListener.call(`RESOLVE_${task}`, isConsumed);
      });
      this.inputStackListener.trigger("ADD_INPUT_ACTION", input, formattedUuid, "", inputType, context);
      return () => {
        this.inputStackListener.trigger("REMOVE_INPUT_ACTION", input, formattedUuid);
        this.inputActionMap.delete(uuid);
      };
    }
  };
  __name(_InputStackListener, "InputStackListener");
  var InputStackListener = _InputStackListener;
  var _InternalInputManager = class _InternalInputManager {
    constructor(inputActionRecord) {
      this.inputActionRecord = inputActionRecord;
      this.defaultInputType = "released";
      this.defaultScrollAxis = "all";
      this.defaultScrollSpeedFactor = 10;
      this.inputStackListener = new InputStackListener();
    }
    //#region Add input action
    /**
     * Add an action that will be executed when the given input is triggered.
     * @param inputAction The input that, when triggered, will execute the action.
     * @param callback The action callback that will be called when the input is triggered.
     * @param options The optional input action options.
     * @returns A callback that has to be called when the input action is not needed anymore.
     */
    addInputAction(inputAction, callback, options) {
      var _a13;
      return this.inputStackListener.addInputAction(
        this.inputActionRecord[inputAction],
        callback,
        (_a13 = options == null ? void 0 : options.inputType) != null ? _a13 : this.defaultInputType
      );
    }
    //#endregion
    //#region On hover
    /**
     * Add an action that will be executed when the given input is triggered on hover of the given HTML element.
     * @param element The HTML element that will allow the action to be executed when hovered on.
     * @param inputAction The input that, when triggered, will execute the action.
     * @param callback The action callback that will be called when the input is triggered.
     * @param options The optional input action options.
     * @returns A callback that has to be called when the input action is not needed anymore.
     */
    addInputActionOnHover(element, inputAction, callback, options) {
      let inputActionDestructor = null;
      const mouseEnterCallback = /* @__PURE__ */ __name(() => {
        var _a13;
        if (inputActionDestructor) {
          inputActionDestructor();
        }
        inputActionDestructor = this.inputStackListener.addInputAction(
          this.inputActionRecord[inputAction],
          callback,
          (_a13 = options == null ? void 0 : options.inputType) != null ? _a13 : this.defaultInputType
        );
      }, "mouseEnterCallback");
      const mouseLeaveCallback = /* @__PURE__ */ __name(() => {
        inputActionDestructor == null ? void 0 : inputActionDestructor();
      }, "mouseLeaveCallback");
      element.addEventListener("mouseenter", mouseEnterCallback);
      element.addEventListener("mouseleave", mouseLeaveCallback);
      return () => {
        element.removeEventListener("mouseenter", mouseEnterCallback);
        element.removeEventListener("mouseleave", mouseLeaveCallback);
        inputActionDestructor == null ? void 0 : inputActionDestructor();
      };
    }
    //#endregion
  };
  __name(_InternalInputManager, "InternalInputManager");
  var InternalInputManager = _InternalInputManager;
  var _InputManager = class _InputManager extends InternalInputManager {
    constructor() {
      super(InputActions);
    }
    //#region Scroll
    /**
     * Add scroll input actions using the gamepad joystick for a given HTML element.
     * @param element The scrollable HTML element.
     * @param options The optional scroll input action options.
     * @returns A callback that has to be called when the scroll input action is not needed anymore.
     */
    addScrollInputAction(element, options) {
      var _a13;
      const scrollSpeed = options == null ? void 0 : options.scrollSpeed;
      let predicate;
      if (scrollSpeed === void 0) {
        predicate = /* @__PURE__ */ __name((axis) => this.computeScrollSpeedFactor(axis, this.defaultScrollSpeedFactor), "predicate");
      } else if (typeof scrollSpeed === "number") {
        predicate = /* @__PURE__ */ __name((axis) => this.computeScrollSpeedFactor(axis, scrollSpeed), "predicate");
      } else {
        predicate = scrollSpeed;
      }
      switch ((_a13 = options == null ? void 0 : options.scrollAxis) != null ? _a13 : this.defaultScrollAxis) {
        case "vertical":
        case "y":
          return this.setupVerticalScroll(element, predicate);
        case "horizontal":
        case "x":
          return this.setupHorizontalScroll(element, predicate);
        default: {
          const verticalScrollDestructor = this.setupVerticalScroll(element, predicate);
          const horizontalScrollDestructor = this.setupHorizontalScroll(element, predicate);
          return () => {
            verticalScrollDestructor();
            horizontalScrollDestructor();
          };
        }
      }
    }
    setupVerticalScroll(element, predicate) {
      const scrollUpInputActionDestructor = this.addInputActionOnHover(
        element,
        InputAction.SCROLL_UP,
        (axisValue) => {
          if (axisValue < 0) {
            return false;
          }
          element.scroll({
            top: element.scrollTop - predicate(axisValue)
          });
          return true;
        },
        { inputType: "axis" }
      );
      const scrollDownInputActionDestructor = this.addInputActionOnHover(
        element,
        InputAction.SCROLL_DOWN,
        (axisValue) => {
          if (axisValue < 0) {
            return false;
          }
          element.scroll({
            top: element.scrollTop + predicate(axisValue)
          });
          return true;
        },
        { inputType: "axis" }
      );
      return () => {
        scrollUpInputActionDestructor();
        scrollDownInputActionDestructor();
      };
    }
    setupHorizontalScroll(element, predicate) {
      const scrollLeftInputActionDestructor = this.addInputActionOnHover(
        element,
        InputAction.SCROLL_LEFT,
        (axisValue) => {
          if (axisValue < 0) {
            return false;
          }
          element.scroll({
            left: element.scrollLeft - predicate(axisValue)
          });
          return true;
        },
        { inputType: "axis" }
      );
      const scrollRightInputActionDestructor = this.addInputActionOnHover(
        element,
        InputAction.SCROLL_RIGHT,
        (axisValue) => {
          if (axisValue < 0) {
            return false;
          }
          element.scroll({
            left: element.scrollLeft + predicate(axisValue)
          });
          return true;
        },
        { inputType: "axis" }
      );
      return () => {
        scrollLeftInputActionDestructor();
        scrollRightInputActionDestructor();
      };
    }
    computeScrollSpeedFactor(axisValue, factor) {
      return axisValue * axisValue * factor;
    }
    //#endregion
  };
  __name(_InputManager, "InputManager");
  var InputManager = _InputManager;
  var _a3;
  var _GamepadUiComponent = (_a3 = class extends import_msfs_sdk.DisplayComponent {
    constructor() {
      var _a13, _b;
      super(...arguments);
      this.classSubscriptions = [];
      this.gamepadUiComponentRef = import_msfs_sdk.FSComponent.createRef();
      this.inputManager = new InputManager();
      this.areGamepadInputsEnabled = import_msfs_sdk.Subject.create(false);
      this._nextHandler = import_msfs_sdk.Subject.create(void 0);
      this.nextHandler = this._nextHandler;
      this.disabled = import_msfs_sdk.SubscribableUtils.toSubscribable((_a13 = this.props.disabled) != null ? _a13 : false, true);
      this.visible = import_msfs_sdk.SubscribableUtils.toSubscribable((_b = this.props.visible) != null ? _b : true, true);
      this.componentClickListener = this.handleComponentClick.bind(this);
    }
    setNextGamepadEventHandler(ref) {
      this._nextHandler.set(ref);
    }
    deletePreviousGamepadEventHandler() {
      throw new Error("Method not implemented.");
    }
    handleGamepadEvent(gamepadEvent) {
      console.log(`Received ${gamepadEvent} in handleMoveEvent`);
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      const gamepadUiComponentInstance = this.gamepadUiComponentRef.getOrDefault();
      if (gamepadUiComponentInstance === null) {
        return;
      }
      const classes = mergeClassProp(this.props.class);
      Object.keys(classes).forEach((className) => {
        const subscribable = import_msfs_sdk.SubscribableUtils.toSubscribable(classes[className], true);
        this.classSubscriptions.push(
          subscribable.sub((value2) => {
            gamepadUiComponentInstance.classList.toggle(className, value2);
          }, true)
        );
      });
      this.isVisibleSubscription = this.visible.sub((isVisible) => {
        gamepadUiComponentInstance.hidden = !isVisible;
      }, true);
      if (import_msfs_sdk.SubscribableUtils.isSubscribable(this.props.disabled)) {
        this.isDisabledSubscription = this.props.disabled.sub((disabled) => {
          disabled ? this.disable() : this.enable();
        }, true);
      } else {
        this.props.disabled ? this.disable() : this.enable();
      }
      window.addEventListener("click", this.componentClickListener);
      if (this.props.onboardingStepId !== void 0) {
        gamepadUiComponentInstance.setAttribute("id", this.props.onboardingStepId);
      }
    }
    handleComponentClick(e) {
      if (!this.gamepadUiComponentRef.instance.contains(e.target)) {
        this.onClickOutOfComponent(e);
      }
    }
    onButtonAPressed() {
      if (this.props.onButtonAPressed !== void 0) {
        this.props.onButtonAPressed();
      }
    }
    onButtonBPressed() {
      if (this.props.onButtonBPressed !== void 0) {
        this.props.onButtonBPressed();
      }
    }
    onClickOutOfComponent(_e) {
      return;
    }
    enableGamepadInputs() {
      if (this.areGamepadInputsEnabled.get()) {
        console.warn("Trying to enable gamepad inputs that are already enabled. Operation cancelled.");
        return;
      }
      this.areGamepadInputsEnabled.set(true);
    }
    disableGamepadInputs() {
      this.areGamepadInputsEnabled.set(false);
    }
    enable() {
      this.gamepadUiComponentRef.instance.removeAttribute("disabled");
    }
    disable() {
      this.gamepadUiComponentRef.instance.setAttribute("disabled", "");
    }
    show() {
      this.gamepadUiComponentRef.instance.style.visibility = "visible";
    }
    hide() {
      this.gamepadUiComponentRef.instance.style.visibility = "hidden";
    }
    toggleFocus(value2) {
      if (value2 !== void 0) {
        if (value2 === true) {
          this.gamepadUiComponentRef.instance.classList.add(_a3.FOCUS_CLASS);
        } else {
          this.gamepadUiComponentRef.instance.classList.remove(_a3.FOCUS_CLASS);
        }
        return;
      }
      this.gamepadUiComponentRef.instance.classList.toggle(_a3.FOCUS_CLASS);
    }
    getComponentRect() {
      return this.gamepadUiComponentRef.instance.getBoundingClientRect();
    }
    destroy() {
      var _a13, _b;
      this.classSubscriptions.forEach((subscription) => subscription.destroy());
      (_a13 = this.isVisibleSubscription) == null ? void 0 : _a13.destroy();
      (_b = this.isDisabledSubscription) == null ? void 0 : _b.destroy();
      window.removeEventListener("click", this.componentClickListener);
      super.destroy();
    }
  }, __name(_a3, "_GamepadUiComponent"), _a3);
  _GamepadUiComponent.FOCUS_CLASS = "focus";
  var GamepadUiComponent = _GamepadUiComponent;
  var _GamepadUiParser = class _GamepadUiParser {
    constructor() {
      this.gamepadUiViewVNode = null;
    }
    /** On veut focus le premier élément */
    bindVNodeReference(gamepadUiViewVNode) {
      this.gamepadUiViewVNode = gamepadUiViewVNode;
      this.focusFirstElement();
    }
    focusFirstElement() {
      if (this.currentElement = this.findFirstElement()) {
        this.currentElement.instance.toggleFocus(true);
      }
    }
    goUp() {
      this.goDir("UP");
    }
    goRight() {
      this.goDir("RIGHT");
    }
    goDown() {
      this.goDir("DOWN");
    }
    goLeft() {
      this.goDir("LEFT");
    }
    pushButtonA() {
      var _a13;
      (_a13 = this.currentElement) == null ? void 0 : _a13.instance.onButtonAPressed();
    }
    pushButtonB() {
      var _a13;
      (_a13 = this.currentElement) == null ? void 0 : _a13.instance.onButtonBPressed();
    }
    goDir(dir) {
      const HTMLElementList = this.parseDOM();
      if (!this.currentElement || HTMLElementList === null) {
        return;
      }
      const nextElement = this.findClosestNode(this.currentElement, HTMLElementList, dir);
      if (nextElement !== null) {
        this.currentElement.instance.toggleFocus(false);
        this.currentElement = nextElement;
        this.currentElement.instance.toggleFocus(true);
      }
    }
    parseDOM() {
      if (this.gamepadUiViewVNode === null) {
        throw new Error(`Can't parse DOM, VNode is null`);
      }
      const VNodeList = [];
      import_msfs_sdk.FSComponent.visitNodes(this.gamepadUiViewVNode, (node) => {
        if (node.instance instanceof GamepadUiComponent) {
          const ref = import_msfs_sdk.FSComponent.createRef();
          ref.instance = node.instance;
          VNodeList.push(ref);
        }
        return false;
      });
      return VNodeList.length ? VNodeList : null;
    }
    findFirstElement() {
      const HTMLElementList = this.parseDOM();
      if (HTMLElementList === null) {
        return null;
      }
      return HTMLElementList[0];
    }
    /** On réduit la liste dont le top est en dessous du bottom du current node. */
    findClosestNode(currentGamepadUiComponent, gamepadUiComponentList, direction) {
      let candidateIndex = -1;
      switch (direction) {
        case "UP":
          candidateIndex = this.findBestCandidate(
            currentGamepadUiComponent,
            gamepadUiComponentList,
            "bottom",
            "top"
          );
          break;
        case "RIGHT":
          candidateIndex = this.findBestCandidate(
            currentGamepadUiComponent,
            gamepadUiComponentList,
            "left",
            "right"
          );
          break;
        case "DOWN":
          candidateIndex = this.findBestCandidate(
            currentGamepadUiComponent,
            gamepadUiComponentList,
            "top",
            "bottom"
          );
          break;
        case "LEFT":
          candidateIndex = this.findBestCandidate(
            currentGamepadUiComponent,
            gamepadUiComponentList,
            "right",
            "left"
          );
          break;
      }
      if (candidateIndex !== -1 && candidateIndex < gamepadUiComponentList.length) {
        return gamepadUiComponentList[candidateIndex];
      }
      return null;
    }
    findBestCandidate(currentGamepadUiComponent, gamepadUiComponentList, side1, side2) {
      const htmlElementCandidates = [];
      let closestDistance = Infinity;
      let candidateIndex = -1;
      const currentHTMLElementRect = currentGamepadUiComponent.instance.getComponentRect();
      gamepadUiComponentList.forEach((htmlElement, index2) => {
        const htmlElementRect = htmlElement.instance.getComponentRect();
        if (this.isRectPosValid(htmlElementRect, currentHTMLElementRect, side1, side2, false)) {
          if (!htmlElementCandidates.some(
            (htmlElementCandidate) => this.isRectPosValid(
              htmlElementRect,
              htmlElementCandidate.instance.getComponentRect(),
              side1,
              side1,
              true
            )
          )) {
            htmlElementCandidates.push(htmlElement);
            const corner = side1 === "bottom" || side1 === "top" ? "left" : "top";
            const distance = this.distances1D(htmlElementRect, currentHTMLElementRect, corner);
            if (distance <= closestDistance) {
              closestDistance = distance;
              candidateIndex = index2;
            }
          }
        }
      });
      return candidateIndex;
    }
    isRectPosValid(rect1, rect2, side1, side2, strict) {
      if (side1 === "bottom" || side1 === "right") {
        if (strict) {
          return rect1[side1] < rect2[side2];
        } else {
          return rect1[side1] <= rect2[side2];
        }
      } else {
        if (strict) {
          return rect1[side1] > rect2[side2];
        } else {
          return rect1[side1] >= rect2[side2];
        }
      }
    }
    distances1D(rect1, rect2, side) {
      return Math.abs(rect2[side] - rect1[side]);
    }
  };
  __name(_GamepadUiParser, "GamepadUiParser");
  var GamepadUiParser = _GamepadUiParser;
  var _UiView = class _UiView extends import_msfs_sdk.DisplayComponent {
    constructor() {
      super(...arguments);
      this.services = [];
    }
    onOpen() {
      var _a13;
      for (const service of this.services) {
        (_a13 = service.onOpen) == null ? void 0 : _a13.call(service);
      }
    }
    onClose() {
      var _a13;
      for (const service of this.services) {
        (_a13 = service.onClose) == null ? void 0 : _a13.call(service);
      }
    }
    onResume() {
      var _a13;
      for (const service of this.services) {
        (_a13 = service.onResume) == null ? void 0 : _a13.call(service);
      }
    }
    onPause() {
      var _a13;
      for (const service of this.services) {
        (_a13 = service.onPause) == null ? void 0 : _a13.call(service);
      }
    }
    onUpdate(time) {
      var _a13;
      for (const service of this.services) {
        (_a13 = service.onUpdate) == null ? void 0 : _a13.call(service, time);
      }
    }
    destroy() {
      var _a13;
      for (const service of this.services) {
        (_a13 = service.destroy) == null ? void 0 : _a13.call(service);
      }
      super.destroy();
    }
  };
  __name(_UiView, "UiView");
  var UiView = _UiView;
  var _GamepadUiView = class _GamepadUiView extends UiView {
    constructor() {
      super(...arguments);
      this.gamepadUiViewRef = import_msfs_sdk.FSComponent.createRef();
      this.gamepadUiParser = new GamepadUiParser();
      this._nextHandler = import_msfs_sdk.Subject.create(void 0);
      this.nextHandler = this._nextHandler;
      this.inputManager = new InputManager();
      this.gamepadComponentChildren = [];
      this.areGamepadInputsEnabled = import_msfs_sdk.Subject.create(false);
      this.viewCoreInputManager = new InternalInputManager(InternalInputActions);
    }
    /**
     * Enable the gamepad inputs for this UI view.
     * Gamepad inputs of any UI view shall be added in its own enableGamepadInputs function via inputManager.
     */
    enableGamepadInputs() {
      if (this.areGamepadInputsEnabled.get()) {
        console.warn("Trying to enable gamepad inputs that are already enabled. Operation cancelled.");
        return;
      }
      this.goBackActionDestructor = this.viewCoreInputManager.addInputAction(
        InternalInputAction.BACK,
        () => {
          if (this.props.appViewService === void 0) {
            return false;
          }
          try {
            this.props.appViewService.goBack();
          } catch (e) {
            return false;
          }
          return true;
        }
      );
      this.gamepadComponentChildren.forEach((child) => child.enableGamepadInputs());
      this.areGamepadInputsEnabled.set(true);
    }
    /**
     * Disable the gamepad inputs for this UI view.
     * Gamepad input destructors of any UI view shall be called in its own disableGamepadInputs function.
     */
    disableGamepadInputs() {
      var _a13;
      (_a13 = this.goBackActionDestructor) == null ? void 0 : _a13.call(this);
      this.gamepadComponentChildren.forEach((child) => child.disableGamepadInputs());
      this.areGamepadInputsEnabled.set(false);
    }
    onClose() {
      super.onClose();
      this.disableGamepadInputs();
    }
    onResume() {
      super.onResume();
      this.enableGamepadInputs();
    }
    onPause() {
      super.onPause();
      this.disableGamepadInputs();
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.gamepadUiParser.bindVNodeReference(node);
      import_msfs_sdk.FSComponent.visitNodes(node, (childNode) => {
        if (childNode.instance instanceof GamepadUiComponent) {
          this.gamepadComponentChildren.push(childNode.instance);
        }
        return false;
      });
    }
    destroy() {
      super.destroy();
      this.disableGamepadInputs();
    }
    setNextGamepadEventHandler(ref) {
      this._nextHandler.set(ref);
    }
    deletePreviousGamepadEventHandler() {
      this._nextHandler.set(void 0);
    }
    handleGamepadEvent(_gamepadEvent) {
      if (_gamepadEvent === GamepadEvents.BUTTON_B) {
        this.gamepadUiParser.pushButtonB();
      }
      const nextHandler = this._nextHandler.get();
      if (nextHandler !== void 0) {
        return nextHandler.handleGamepadEvent(_gamepadEvent);
      }
      switch (_gamepadEvent) {
        case GamepadEvents.JOYDIR_UP:
          this.gamepadUiParser.goUp();
          break;
        case GamepadEvents.JOYDIR_RIGHT:
          this.gamepadUiParser.goRight();
          break;
        case GamepadEvents.JOYDIR_DOWN:
          this.gamepadUiParser.goDown();
          break;
        case GamepadEvents.JOYDIR_LEFT:
          this.gamepadUiParser.goLeft();
          break;
        case GamepadEvents.BUTTON_A:
          this.gamepadUiParser.pushButtonA();
          break;
      }
    }
  };
  __name(_GamepadUiView, "GamepadUiView");
  var GamepadUiView = _GamepadUiView;
  var ViewBootMode = /* @__PURE__ */ ((ViewBootMode2) => {
    ViewBootMode2[ViewBootMode2["COLD"] = 0] = "COLD";
    ViewBootMode2[ViewBootMode2["HOT"] = 1] = "HOT";
    return ViewBootMode2;
  })(ViewBootMode || {});
  var ViewSuspendMode = /* @__PURE__ */ ((ViewSuspendMode2) => {
    ViewSuspendMode2[ViewSuspendMode2["SLEEP"] = 0] = "SLEEP";
    ViewSuspendMode2[ViewSuspendMode2["TERMINATE"] = 1] = "TERMINATE";
    return ViewSuspendMode2;
  })(ViewSuspendMode || {});
  var defaultViewOptions = {
    BootMode: ViewBootMode.COLD,
    SuspendMode: ViewSuspendMode.SLEEP
  };
  var _AppViewService = class _AppViewService {
    constructor(bus) {
      this.registeredUiViewEntries = /* @__PURE__ */ new Map();
      this.appViewStack = [];
      this.registeredUiViewPromises = [];
      this.hasInitialized = false;
      this._currentUiView = import_msfs_sdk.Subject.create(null);
      this.currentUiView = this._currentUiView;
      this.bus = bus;
      this.goHomeSub = this.bus.on(
        "_efb_appviewservice_go_home",
        () => {
          const steps = this.appViewStack.length - 1;
          if (steps >= 1) {
            this.goBack(steps);
          }
        },
        true
      );
    }
    onOpen() {
      var _a13, _b;
      this.goHomeSub.resume();
      (_b = (_a13 = this.getActiveUiViewEntry()) == null ? void 0 : _a13.ref) == null ? void 0 : _b.onOpen();
    }
    onClose() {
      var _a13, _b;
      this.goHomeSub.pause();
      (_b = (_a13 = this.getActiveUiViewEntry()) == null ? void 0 : _a13.ref) == null ? void 0 : _b.onClose();
    }
    onResume() {
      var _a13, _b;
      this.goHomeSub.resume();
      (_b = (_a13 = this.getActiveUiViewEntry()) == null ? void 0 : _a13.ref) == null ? void 0 : _b.onResume();
    }
    onPause() {
      var _a13, _b;
      this.goHomeSub.pause();
      (_b = (_a13 = this.getActiveUiViewEntry()) == null ? void 0 : _a13.ref) == null ? void 0 : _b.onPause();
    }
    /**
     * Registers and renders a view (page or popup) to be opened by the service.
     * @param key The UiView string key.
     * @param type The view type
     * @param vNodeFactory A function that returns a {@link UiView} VNode for the key
     * @param options The {@link UiView} {@link ViewOptions}
     * @returns UiViewEntry
     */
    registerView(key, type, vNodeFactory, options) {
      if (this.registeredUiViewEntries.has(key)) {
        throw new Error(`View "${key}" is already used`);
      } else if (typeof vNodeFactory !== "function") {
        throw new Error("vNodeFactory has to be a function returning a VNode");
      }
      const viewOptions = Object.assign({}, defaultViewOptions, options);
      const appViewEntry = {
        key,
        render: vNodeFactory,
        vNode: vNodeFactory,
        ref: null,
        containerRef: import_msfs_sdk.FSComponent.createRef(),
        isVisible: import_msfs_sdk.Subject.create(false),
        layer: import_msfs_sdk.Subject.create(-1),
        type: import_msfs_sdk.Subject.create(type),
        isInit: false,
        viewOptions
      };
      if (viewOptions.BootMode === ViewBootMode.HOT) {
        this.initViewEntry(appViewEntry);
      }
      this.registeredUiViewEntries.set(key, appViewEntry);
      return appViewEntry;
    }
    registerPage(key, vNodeFactory, options) {
      return this.registerView(key, "page", vNodeFactory, options);
    }
    registerPopup(key, vNodeFactory, options) {
      return this.registerView(key, "popup", vNodeFactory, options);
    }
    async killView(viewEntry) {
      var _a13;
      if (viewEntry.isInit === false) {
        return;
      }
      if (this.appViewStack.find((view) => view.key === viewEntry.key)) {
        throw new Error("You must close your view before killing it");
      }
      viewEntry.isInit = false;
      (_a13 = viewEntry.ref) == null ? void 0 : _a13.destroy();
      viewEntry.containerRef.instance.destroy();
      viewEntry.ref = null;
      viewEntry.containerRef = import_msfs_sdk.FSComponent.createRef();
      viewEntry.vNode = viewEntry.render.bind(viewEntry);
      return new Promise((resolve) => {
        if (viewEntry.viewOptions.BootMode === ViewBootMode.HOT) {
          this.initViewEntry(viewEntry);
        }
        resolve();
      });
    }
    /**
     * @param entry a {@link UiViewEntry}
     * @param shouldOpen opens the view on initialization, defaults to false
     */
    initViewEntry(entry, shouldOpen = false) {
      var _a13;
      if (entry.isInit) {
        return;
      }
      entry.isInit = true;
      entry.vNode = value(entry.vNode);
      entry.ref = entry.vNode.instance;
      (_a13 = this.appViewRef) == null ? void 0 : _a13.renderView(
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
          AppViewWrapper,
          {
            viewName: entry.key,
            isVisible: entry.isVisible,
            type: entry.type,
            layer: entry.layer,
            ref: entry.containerRef
          },
          entry.vNode
        )
      );
      shouldOpen && entry.ref.onOpen();
    }
    /**
     * Destroys every view in registered view entries and resets the view stack.
     */
    unload() {
      var _a13;
      this.registeredUiViewPromises = [];
      for (const viewEntry of this.registeredUiViewEntries.values()) {
        (_a13 = viewEntry.ref) == null ? void 0 : _a13.destroy();
      }
      this.registeredUiViewEntries.clear();
      this.appViewStack.splice(0, this.appViewStack.length);
      this.hasInitialized = false;
    }
    /**
     *
     * @param homePageUiViewKey the string key of the {@link UiView}
     * @returns a Promise resolving when all pages are initialized
     */
    async initialize(homePageUiViewKey) {
      if (this.hasInitialized) {
        return Promise.resolve();
      }
      return Promise.all(this.registeredUiViewPromises).then(() => {
        this.initializeAppViewStack(homePageUiViewKey);
        this.hasInitialized = true;
      });
    }
    /**
     * @returns the current active view entry.
     */
    getActiveUiViewEntry() {
      const activeViewEntry = import_msfs_sdk.ArrayUtils.peekLast(this.appViewStack);
      return activeViewEntry;
    }
    /** @deprecated */
    on(_event, _viewKey, _callback) {
      return this;
    }
    /**
     * Handles logic associated with changing the open page.
     * @param page Page to close
     */
    handleCloseView(page) {
      var _a13;
      if (!page || page.isVisible.get() === false) return;
      (_a13 = page.ref) == null ? void 0 : _a13.onPause();
      page.isVisible.set(false);
      this._currentUiView.set(null);
      if (page.viewOptions.SuspendMode === ViewSuspendMode.TERMINATE) {
        setTimeout(() => this.killView(page));
      }
    }
    /**
     * Handles logic associated with changing the open page.
     * @param page Page to open
     */
    handleOpenView(page) {
      var _a13;
      if (!page || page.isVisible.get() === true) return;
      (_a13 = page.ref) == null ? void 0 : _a13.onResume();
      page.isVisible.set(true);
      this._currentUiView.set(page);
    }
    /**
     * Populate the view stack with its respective home page.
     * @param mainPageUiViewKey the key of the home page
     */
    initializeAppViewStack(mainPageUiViewKey) {
      this.open(mainPageUiViewKey);
    }
    /**
     * @param key the {@link UiView} string key
     * @returns the {@link UiViewEntry} corresponding to the key
     * @throws if the {@link UiViewEntry} doesn't exists
     */
    getUiViewEntry(key) {
      const appViewEntry = this.registeredUiViewEntries.get(key);
      if (!appViewEntry) {
        throw new Error(`${key} wasn't registered as a view`);
      }
      return appViewEntry;
    }
    /**
     * Called by AppContainer to pass in the refs to the view.
     * Should only be called once.
     * @param appViewRef The app view ref.
     */
    onAppContainerRendered(appViewRef) {
      this.appViewRef = appViewRef;
    }
    isInViewStack(view) {
      const key = typeof view === "string" ? view : view.key;
      return this.appViewStack.findIndex((stackViewEntry) => key === stackViewEntry.key) !== -1;
    }
    isActive(view) {
      const key = typeof view === "string" ? view : view.key;
      const activeViewEntry = this.getActiveUiViewEntry();
      if (activeViewEntry === void 0) {
        return false;
      }
      return key === activeViewEntry.key;
    }
    open(key) {
      const viewEntry = this.advanceViewStack(key);
      return viewEntry;
    }
    goBack(steps) {
      steps != null ? steps : steps = 1;
      if (steps <= 0) {
        throw new RangeError(`Steps must be superior to 0.`);
      } else if (steps >= this.appViewStack.length) {
        throw new RangeError(`Steps can't be superior to ${this.appViewStack.length} when called.`);
      }
      const activeViewEntry = this.getActiveUiViewEntry();
      if (this.appViewStack.length > steps && activeViewEntry) {
        const viewsToOpen = [];
        const viewsToClose = [];
        while (steps--) {
          const view = this.appViewStack.pop();
          if (view) {
            viewsToClose.push(view);
          }
        }
        let i = this.appViewStack.length;
        do {
          viewsToOpen.push(this.appViewStack[--i]);
        } while (i > 0 && this.appViewStack[i].type.get() !== "page");
        for (const page of viewsToClose) {
          this.handleCloseView(page);
        }
        for (const page of viewsToOpen.reverse()) {
          this.handleOpenView(page);
        }
      }
      return this.getActiveUiViewEntry();
    }
    /**
     * Handles view stack logic
     * @param key the {@link UiView} string key
     * @returns the current {@link UiViewEntry}
     */
    advanceViewStack(key) {
      const viewEntry = this.getUiViewEntry(key);
      if (this.appViewStack.includes(viewEntry)) {
        if (this.getActiveUiViewEntry() !== viewEntry) {
          throw new Error("Page or popup is already in the viewstack and can't be opened twice");
        }
        return viewEntry;
      }
      switch (viewEntry.type.get()) {
        case "page":
          for (const page of [...this.appViewStack].reverse()) {
            this.handleCloseView(page);
          }
          break;
      }
      this.appViewStack.push(viewEntry);
      this.openView(viewEntry);
      this.handleOpenView(viewEntry);
      return viewEntry;
    }
    /**
     * Handle logic associated with opening a view
     * @param view the view to open.
     */
    openView(view) {
      if (!view.isInit) {
        this.initViewEntry(view, true);
      }
      const index2 = this.appViewStack.indexOf(view);
      if (index2 < 0) {
        throw new Error(`AppViewService: view not found in view stack: ${view.key}`);
      }
      view.layer.set(index2);
    }
    /**
     * Updates all the pages/popups that are initialized and visible
     * @param time timestamp
     */
    update(time) {
      this.registeredUiViewEntries.forEach((UiView2) => {
        var _a13;
        if (UiView2.isInit && UiView2.isVisible.get()) {
          (_a13 = UiView2.ref) == null ? void 0 : _a13.onUpdate(time);
        }
      });
    }
    /**
     * Routes the event to the current {@link UiView}
     * @param gamepadEvent the {@link GamepadEvents}
     */
    routeGamepadInteractionEvent(gamepadEvent) {
      const _currentUiView = this.getActiveUiViewEntry();
      if ((_currentUiView == null ? void 0 : _currentUiView.ref) instanceof GamepadUiView) {
        _currentUiView.ref.handleGamepadEvent(gamepadEvent);
      }
    }
  };
  __name(_AppViewService, "AppViewService");
  var AppViewService = _AppViewService;
  var _AppViewWrapper = class _AppViewWrapper extends import_msfs_sdk.DisplayComponent {
    constructor() {
      super(...arguments);
      this.rootRef = import_msfs_sdk.FSComponent.createRef();
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "div",
        {
          ref: this.rootRef,
          class: {
            "ui-view": true,
            [this.props.viewName]: true,
            hidden: this.props.isVisible.map(import_msfs_sdk.SubscribableMapFunctions.not()),
            page: this.props.type.map(where("page")),
            popup: this.props.type.map(where("popup"))
          },
          style: {
            "z-index": this.props.layer.map(toString())
          }
        },
        this.props.children
      );
    }
    destroy() {
      var _a13;
      const root = this.rootRef.getOrDefault();
      if (root !== null) {
        (_a13 = root.parentNode) == null ? void 0 : _a13.removeChild(root);
      }
      super.destroy();
    }
  };
  __name(_AppViewWrapper, "AppViewWrapper");
  var AppViewWrapper = _AppViewWrapper;
  var __defProp22 = Object.defineProperty;
  var __getOwnPropDesc2 = Object.getOwnPropertyDescriptor;
  var __decorateClass = /* @__PURE__ */ __name((decorators, target, key, kind) => {
    var result = __getOwnPropDesc2(target, key);
    for (var i = decorators.length - 1, decorator; i >= 0; i--)
      if (decorator = decorators[i])
        result = decorator(target, key, result) || result;
    if (result) __defProp22(target, key, result);
    return result;
  }, "__decorateClass");
  var _AppView = class _AppView extends import_msfs_sdk.DisplayComponent {
    constructor(props) {
      var _a13;
      super(props);
      this.rootRef = import_msfs_sdk.FSComponent.createRef();
      this.pageKeyActions = /* @__PURE__ */ new Map();
      this._appViewService = props.appViewService || (props.bus ? new AppViewService(props.bus) : void 0);
      this._bus = (_a13 = this._appViewService) == null ? void 0 : _a13.bus;
      this._unitsSettingManager = props.unitsSettingManager;
      this._efbSettingsManager = props.efbSettingsManager;
      this._notificationManager = props.notificationManager;
      this._onboardingManager = props.onboardingManager;
    }
    get appViewService() {
      const _appViewService = this._appViewService;
      if (!_appViewService) {
        throw new Error(
          `Cannot resolve 'appViewService' because none of 'appViewService' and 'bus' props were given.`
        );
      }
      return _appViewService;
    }
    get bus() {
      const _bus = this._bus;
      if (!_bus) {
        throw new Error(`Cannot resolve 'bus' because none of 'appViewService' and 'bus' props were given.`);
      }
      return _bus;
    }
    get unitsSettingManager() {
      const _unitsSettingManager = this._unitsSettingManager;
      if (_unitsSettingManager === void 0) {
        throw new Error(`Cannot resolve 'unitsSettingManager'`);
      }
      return _unitsSettingManager;
    }
    get efbSettingsManager() {
      const _efbSettingsManager = this._efbSettingsManager;
      if (_efbSettingsManager === void 0) {
        throw new Error(`Cannot resolve 'efbSettingsManager'`);
      }
      return _efbSettingsManager;
    }
    /**
     * EFB notification manager
     * @returns a unique efbNotificationManager instance
     */
    get notificationManager() {
      const notificationManager = this._notificationManager;
      if (!notificationManager) {
        throw new Error("Notification manager is not defined");
      }
      return notificationManager;
    }
    get onboardingManager() {
      const _onboardingManager = this._onboardingManager;
      if (!_onboardingManager) {
        throw new Error(`Cannot resolve 'onboardingManager'`);
      }
      return _onboardingManager;
    }
    /**
     * Called once when the view is opened for the first time.
     */
    onOpen() {
      var _a13;
      (_a13 = this._appViewService) == null ? void 0 : _a13.onOpen();
    }
    /**
     * Called once when the view is destroyed.
     */
    onClose() {
      var _a13;
      (_a13 = this._appViewService) == null ? void 0 : _a13.onClose();
    }
    /**
     * Called each time the view is resumed.
     */
    onResume() {
      var _a13;
      (_a13 = this._appViewService) == null ? void 0 : _a13.onResume();
    }
    /**
     * Called each time the view is closed.
     */
    onPause() {
      var _a13;
      (_a13 = this._appViewService) == null ? void 0 : _a13.onPause();
    }
    /**
     * On Update loop - It update the `AppViewService` if it is used.
     * @param time in milliseconds
     */
    onUpdate(time) {
      var _a13;
      (_a13 = this._appViewService) == null ? void 0 : _a13.update(time);
    }
    /**
     * Callback to register all views the app might use.
     */
    registerViews() {
    }
    /**
     * @internal
     * TODO : Need to be documented after Gamepad integration
     */
    routeGamepadInteractionEvent(gamepadEvent) {
      var _a13;
      (_a13 = this._appViewService) == null ? void 0 : _a13.routeGamepadInteractionEvent(gamepadEvent);
    }
    /**
     * @internal
     * @param key custom page key defined in `pageKeyActions`
     * @param args array of arguments given to the defined callback
     */
    handlePageKeyAction(key, args) {
      if (key === null) {
        return;
      }
      const action = this.pageKeyActions.get(key);
      if (action === void 0) {
        console.error(`Key "${key}" doesn't exists.`);
        return;
      }
      toPromise(action(args)).catch((reason) => {
        console.error(`handlePageKeyAction Error for key "${key}"`, reason);
      });
    }
    /**
     * If using EFB's AppViewService, this method returns an AppContainer binded to AppViewService.
     * Otherwise it can be customized with plain JSX/TSX or custom view service, etc...
     *
     * @example
     * Surrounding AppContainer with a custom class:
     * ```ts
     * public render(): TVNode<HTMLDivElement> {
     * 	return <div class="my-custom-class">{super.render()}</div>;
     * }
     * ```
     * @example
     * Here's an plain JSX/TSX example:
     * ```ts
     * public render(): TVNode<HTMLSpanElement> {
     * 	return <span>Hello World!</span>;
     * }
     * ```
     */
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(AppContainer, { appViewService: this.appViewService });
    }
    /** @inheritdoc */
    onAfterRender(node) {
      var _a13;
      super.onAfterRender(node);
      this.registerViews();
      if (this.defaultView) {
        (_a13 = this._appViewService) == null ? void 0 : _a13.initialize(this.defaultView);
      }
    }
    /** @internal */
    destroy() {
      var _a13;
      (_a13 = this._appViewService) == null ? void 0 : _a13.unload();
      super.destroy();
    }
  };
  __name(_AppView, "AppView");
  var AppView = _AppView;
  __decorateClass([
    Memoize()
  ], AppView.prototype, "appViewService");
  __decorateClass([
    Memoize()
  ], AppView.prototype, "bus");
  __decorateClass([
    Memoize()
  ], AppView.prototype, "unitsSettingManager");
  __decorateClass([
    Memoize()
  ], AppView.prototype, "efbSettingsManager");
  __decorateClass([
    Memoize()
  ], AppView.prototype, "onboardingManager");
  var _Button = class _Button extends GamepadUiComponent {
    constructor() {
      var _a13, _b, _c, _d;
      super(...arguments);
      this.isButtonDisabled = import_msfs_sdk.SubscribableUtils.toSubscribable((_a13 = this.props.disabled) != null ? _a13 : false, true);
      this.isButtonHoverable = import_msfs_sdk.MappedSubject.create(
        ([isHoverable, isDisabled]) => {
          return isHoverable && !isDisabled;
        },
        import_msfs_sdk.SubscribableUtils.toSubscribable((_b = this.props.hoverable) != null ? _b : true, true),
        this.isButtonDisabled
      );
      this.isButtonSelected = import_msfs_sdk.MappedSubject.create(
        ([isSelected, state]) => isSelected || state,
        import_msfs_sdk.SubscribableUtils.toSubscribable((_c = this.props.selected) != null ? _c : false, true),
        import_msfs_sdk.SubscribableUtils.toSubscribable((_d = this.props.state) != null ? _d : false, true)
      );
    }
    /**
     * @deprecated Old way to render the button. Instead of extending the `Button` class, render your content as a children of `<Button>...</Button>`.
     */
    buttonRender() {
      return null;
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "button",
        {
          ref: this.gamepadUiComponentRef,
          class: mergeClassProp(
            "button",
            "abstract-button",
            // deprecated
            {
              hoverable: this.isButtonHoverable,
              selected: this.isButtonSelected
            },
            this.props.class
          ),
          style: this.props.style
        },
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "disabled-layer" }),
        this.props.children,
        this.buttonRender()
      );
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      if (this.props.callback) {
        this.gamepadUiComponentRef.instance.onclick = (event) => {
          var _a13;
          if (this.isButtonDisabled.get() === true) {
            return;
          }
          (_a13 = this.props.callback) == null ? void 0 : _a13.call(this, event);
        };
      }
    }
    destroy() {
      this.isButtonHoverable.destroy();
      super.destroy();
    }
  };
  __name(_Button, "Button");
  var Button = _Button;
  var _TT = class _TT extends import_msfs_sdk.DisplayComponent {
    constructor() {
      super(...arguments);
      this.ref = import_msfs_sdk.FSComponent.createRef();
      this.key = import_msfs_sdk.SubscribableUtils.toSubscribable(this.props.key, true);
      this.formatter = this.props.format || ((text) => text);
      this.subs = [];
      this.reloadText = this._reloadText.bind(this);
    }
    _reloadText() {
      var _a13;
      const key = this.key.get();
      let translatedKey = key.startsWith("@") || key.startsWith("TT") ? Utils.Translate(key) : key;
      (_a13 = this.props.arguments) == null ? void 0 : _a13.forEach((argumentValue, argumentKey) => {
        const argumentValueSub = import_msfs_sdk.SubscribableUtils.toSubscribable(argumentValue, true);
        translatedKey = translatedKey.replace(argumentKey, argumentValueSub.get());
      });
      this.updateText(translatedKey);
    }
    getTextTransformed(text) {
      if (isFunction(this.formatter)) {
        return this.formatter(text);
      }
      switch (this.formatter) {
        case "upper":
        case "uppercase":
          return text.toUpperCase();
        case "lower":
        case "lowercase":
          return text.toLowerCase();
        case "ucfirst":
          return `${text.charAt(0).toUpperCase()}${text.slice(1)}`;
        case "capitalize":
          return text.replace(/(^\w{1})|(\s+\w{1})/g, (letter) => letter.toUpperCase());
        default:
          console.warn(`Format "${this.props.format}" is not supported.`);
          return text;
      }
    }
    getEmphasisText(text) {
      let emphasisText;
      const boldRegex = /\*\*(.*?)\*\*/g;
      const italicRegex = /\*(.*?)\*/g;
      emphasisText = text.replace(boldRegex, '<span class="bold-text">$1</span>');
      emphasisText = emphasisText.replace(italicRegex, '<span class="italic-text">$1</span>');
      return emphasisText;
    }
    updateText(text) {
      const formattedText = this.getTextTransformed(text);
      const htmlText = this.getEmphasisText(formattedText);
      this.ref.instance.innerHTML = htmlText;
    }
    render() {
      var _a13;
      const Tag2 = (_a13 = this.props.type) != null ? _a13 : "span";
      const _b = this.props, { key: _key, type: _type, format: _format, children: _children } = _b, props = __objRest(_b, ["key", "type", "format", "children"]);
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(Tag2, __spreadValues2({ ref: this.ref }, props));
    }
    onAfterRender(node) {
      var _a13;
      super.onAfterRender(node);
      (_a13 = this.props.arguments) == null ? void 0 : _a13.forEach((argumentValue, argumentKey) => {
        const subValue = import_msfs_sdk.SubscribableUtils.toSubscribable(argumentValue, true);
        this.subs.push(subValue.sub(this.reloadText));
      });
      this.reloadSubscription = this.key.sub(this.reloadText, true);
      Coherent.on("RELOAD_LOCALISATION", this.reloadText);
    }
    destroy() {
      var _a13;
      Coherent.off("RELOAD_LOCALISATION", this.reloadText);
      (_a13 = this.reloadSubscription) == null ? void 0 : _a13.destroy();
      this.subs.forEach((sub) => sub.destroy());
      super.destroy();
    }
  };
  __name(_TT, "TT");
  var TT = _TT;
  var _a4;
  var _GamepadListNavigationManager = (_a4 = class {
    constructor(data, toolbox) {
      var _a13, _b;
      this.data = data;
      this.toolbox = toolbox;
      this.allowedUpcomingNavigationMs = 0;
      this.currentItemIndex = import_msfs_sdk.Subject.create(-1);
      this.isListFocusedSubscriptions = [];
      this.currentItemIndexSubscription = this.currentItemIndex.sub((index2) => {
        var _a22, _b2;
        const item = index2 < 0 || index2 >= this.data.length ? null : this.data.get(index2);
        (_b2 = (_a22 = this.toolbox).onItemHovered) == null ? void 0 : _b2.call(_a22, item, index2);
      });
      this.internalInputManager = new InternalInputManager(InternalInputActions);
      this.navigationBetweenItemsDelayMs = (_a13 = toolbox.navigationBetweenItemsDelayMs) != null ? _a13 : _a4.defaultNavigationBetweenItemsDelayMs;
      if (toolbox.wrap) {
        this.computeUpcomingItemIndex = (previousItemIndex, step) => (this.data.length + previousItemIndex + step) % this.data.length;
      } else {
        this.computeUpcomingItemIndex = (previousItemIndex, step) => {
          const upcomingItemIndex = previousItemIndex + step;
          if (upcomingItemIndex >= 0 && upcomingItemIndex < this.data.length) {
            return upcomingItemIndex;
          } else {
            return previousItemIndex;
          }
        };
      }
      if (import_msfs_sdk.SubscribableUtils.isMutableSubscribable(toolbox.isListFocused)) {
        this.isListFocused = toolbox.isListFocused;
      } else if (import_msfs_sdk.SubscribableUtils.isSubscribable(toolbox.isListFocused)) {
        this.isListFocused = import_msfs_sdk.Subject.create(toolbox.isListFocused.get());
        this.isListFocusedSubscriptions.push(
          toolbox.isListFocused.sub((value2) => this.isListFocused.set(value2))
        );
      } else {
        this.isListFocused = import_msfs_sdk.Subject.create((_b = toolbox.isListFocused) != null ? _b : false);
      }
      this.isListFocusedSubscriptions.push(
        import_msfs_sdk.MappedSubject.create(
          ([is2D, isFocused]) => {
            if (is2D && isFocused) {
              this.enableGamepadNavigation();
            } else {
              this.disableGamepadNavigation();
            }
          },
          window.PanelInfo.is2D,
          this.isListFocused
        )
      );
    }
    navigateThroughList(step) {
      const currentNavigationTryMs = Date.now();
      if (currentNavigationTryMs < this.allowedUpcomingNavigationMs) {
        return false;
      }
      this.allowedUpcomingNavigationMs = currentNavigationTryMs + this.navigationBetweenItemsDelayMs;
      const currentItemIndex = this.currentItemIndex.get();
      if (currentItemIndex === -1) {
        return false;
      }
      const upcomingItemIndex = this.computeUpcomingItemIndex(currentItemIndex, step);
      this.currentItemIndex.set(upcomingItemIndex);
      return true;
    }
    enableGamepadNavigation() {
      Coherent.trigger("SET_EFB_GAMEPAD_NAVIGATION", true);
      this.currentItemIndex.set(0);
      this.unfocusListActionDestructor = this.internalInputManager.addInputAction(
        InternalInputAction.BACK,
        () => {
          this.isListFocused.set(false);
          return true;
        }
      );
      this.selectItemActionDestructor = this.internalInputManager.addInputAction(
        InternalInputAction.VALID,
        () => {
          var _a13, _b;
          const currentItemIndex = this.currentItemIndex.get();
          try {
            (_b = (_a13 = this.toolbox).onItemSelected) == null ? void 0 : _b.call(_a13, this.data.get(currentItemIndex), currentItemIndex);
          } catch (e) {
            return false;
          }
          return true;
        }
      );
      this.allowedUpcomingNavigationMs = Date.now();
      this.prevItemActionDestructor = this.internalInputManager.addInputAction(
        InternalInputAction.UP,
        () => this.navigateThroughList(
          -1
          /* UP */
        ),
        { inputType: "axis" }
      );
      this.nextItemActionDestructor = this.internalInputManager.addInputAction(
        InternalInputAction.DOWN,
        () => this.navigateThroughList(
          1
          /* DOWN */
        ),
        { inputType: "axis" }
      );
    }
    disableGamepadNavigation() {
      var _a13, _b, _c, _d;
      (_a13 = this.unfocusListActionDestructor) == null ? void 0 : _a13.call(this);
      this.unfocusListActionDestructor = void 0;
      (_b = this.selectItemActionDestructor) == null ? void 0 : _b.call(this);
      this.selectItemActionDestructor = void 0;
      (_c = this.prevItemActionDestructor) == null ? void 0 : _c.call(this);
      this.prevItemActionDestructor = void 0;
      (_d = this.nextItemActionDestructor) == null ? void 0 : _d.call(this);
      this.nextItemActionDestructor = void 0;
      this.currentItemIndex.set(-1);
      Coherent.trigger("SET_EFB_GAMEPAD_NAVIGATION", false);
    }
    pause() {
      this.disableGamepadNavigation();
      this.currentItemIndexSubscription.pause();
      this.isListFocusedSubscriptions.forEach((subscription) => {
        subscription.pause();
      });
    }
    resume() {
      this.currentItemIndexSubscription.resume(this.isListFocused.get());
      this.isListFocusedSubscriptions.forEach((subscription) => {
        subscription.resume(true);
      });
    }
    destroy() {
      this.currentItemIndexSubscription.destroy();
      this.isListFocusedSubscriptions.forEach((subscription) => {
        subscription.destroy();
      });
      this.disableGamepadNavigation();
    }
  }, __name(_a4, "_GamepadListNavigationManager"), _a4);
  _GamepadListNavigationManager.defaultNavigationBetweenItemsDelayMs = 100;
  var _IconElement = class _IconElement extends import_msfs_sdk.DisplayComponent {
    constructor() {
      super(...arguments);
      this.url = import_msfs_sdk.SubscribableUtils.toSubscribable(this.props.url, true);
      this.el = import_msfs_sdk.FSComponent.createRef();
      this.onSVGIconLoaded = (found, svgAsString) => {
        this.el.instance.innerText = "";
        if (!found) {
          console.error(`Image ${this.url.get()} was not found`);
          return;
        }
        const template = document.createElement("template");
        template.innerHTML = svgAsString;
        const svgAsHtml = template.content.querySelector("svg");
        if (svgAsHtml) {
          this.el.instance.appendChild(svgAsHtml);
        }
      };
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { ref: this.el, class: mergeClassProp("icon-element", this.props.class) });
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.urlSub = this.url.sub((url) => {
        if (url.endsWith(".svg")) {
          getIconCacheMgr().loadURL(url, this.onSVGIconLoaded);
        } else {
          this.el.instance.innerHTML = `<img src="${url}" onerror="console.error('Image ${url} was not found')" />`;
        }
      }, true);
    }
    destroy() {
      var _a13;
      (_a13 = this.urlSub) == null ? void 0 : _a13.destroy();
      super.destroy();
    }
  };
  __name(_IconElement, "IconElement");
  var IconElement = _IconElement;
  var _IconButton = class _IconButton extends import_msfs_sdk.DisplayComponent {
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        Button,
        {
          class: mergeClassProp("icon-button", this.props.class),
          style: this.props.style,
          callback: this.props.callback,
          hoverable: this.props.hoverable,
          selected: this.props.selected,
          state: this.props.state,
          disabled: this.props.disabled,
          onboardingStepId: this.props.onboardingStepId
        },
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(IconElement, { url: this.props.iconPath })
      );
    }
  };
  __name(_IconButton, "IconButton");
  var IconButton = _IconButton;
  var _TTButton = class _TTButton extends import_msfs_sdk.DisplayComponent {
    render() {
      var _a13;
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        Button,
        {
          class: mergeClassProp("tt-button", "classic-button", this.props.class),
          style: this.props.style,
          callback: this.props.callback,
          hoverable: this.props.hoverable,
          selected: this.props.selected,
          state: this.props.state,
          disabled: this.props.disabled
        },
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
          TT,
          {
            class: "bold-text",
            key: this.props.key,
            type: this.props.type,
            format: (_a13 = this.props.format) != null ? _a13 : "uppercase",
            arguments: this.props.arguments
          }
        )
      );
    }
  };
  __name(_TTButton, "TTButton");
  var TTButton = _TTButton;
  var _TabSelector = class _TabSelector extends GamepadUiComponent {
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "div",
        {
          ref: this.gamepadUiComponentRef,
          class: {
            "tab-selector": true,
            active: this.props.active,
            hidden: this.props.hidden
          }
        },
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "disabled-layer" }),
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(TT, { key: this.props.tabName, class: "text", format: "ucfirst" }),
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "state" })
      );
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      if (this.props.callback) {
        this.gamepadUiComponentRef.instance.onclick = (e) => {
          var _a13;
          if (import_msfs_sdk.SubscribableUtils.toSubscribable(this.props.disabled, true).get() === true) {
            return;
          }
          (_a13 = this.props.callback) == null ? void 0 : _a13.call(e);
        };
      }
    }
  };
  __name(_TabSelector, "TabSelector");
  var TabSelector = _TabSelector;
  var _AccordionButton = class _AccordionButton extends import_msfs_sdk.DisplayComponent {
    constructor() {
      super(...arguments);
      this.iconStyle = import_msfs_sdk.Subject.create("rotate(0)");
      this.isFoldedSubscription = this.props.isFolded.sub((isFolded) => {
        this.iconStyle.set(`rotate(${isFolded ? 0 : -0.5}turn)`);
      }, true);
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        Button,
        {
          class: mergeClassProp("accordion-button", this.props.class),
          style: this.props.style,
          callback: this.props.callback,
          hoverable: this.props.hoverable,
          selected: this.props.selected,
          state: this.props.state,
          disabled: this.props.disabled
        },
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "icon-container", style: { transform: this.iconStyle } }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
          IconElement,
          {
            class: "accordion-button-icon",
            url: `coui://html_ui/efb_ui/efb_os/Assets/icons/Arrow.svg`
          }
        )),
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "title" }, this.props.children)
      );
    }
    destroy() {
      this.isFoldedSubscription.destroy();
      super.destroy();
    }
  };
  __name(_AccordionButton, "AccordionButton");
  var AccordionButton = _AccordionButton;
  var _AbstractAccordion = class _AbstractAccordion extends GamepadUiComponent {
    constructor() {
      var _a13;
      super(...arguments);
      this.isFolded = import_msfs_sdk.SubscribableUtils.isMutableSubscribable(this.props.isFolded) ? this.props.isFolded : import_msfs_sdk.Subject.create((_a13 = this.props.isFolded) != null ? _a13 : true);
    }
    renderBody() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(import_msfs_sdk.FSComponent.Fragment, null, this.props.children);
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: { accordion: true, folded: this.isFolded }, ref: this.gamepadUiComponentRef }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        AccordionButton,
        {
          isFolded: this.isFolded,
          disabled: this.props.disabled,
          class: "accordion-button abstract-button--full-width",
          callback: /* @__PURE__ */ __name(() => {
            this.isFolded.set(!this.isFolded.get());
          }, "callback")
        },
        this.renderHeader()
      ), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: { "menu-content": true, hide: this.isFolded } }, this.renderBody()));
    }
  };
  __name(_AbstractAccordion, "AbstractAccordion");
  var AbstractAccordion = _AbstractAccordion;
  var _ElementAccordion = class _ElementAccordion extends AbstractAccordion {
    renderHeader() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(import_msfs_sdk.FSComponent.Fragment, null, value(this.props.header));
    }
  };
  __name(_ElementAccordion, "ElementAccordion");
  var ElementAccordion = _ElementAccordion;
  var _a5;
  var _BearingDisplay = (_a5 = class extends import_msfs_sdk.AbstractNumberUnitDisplay {
    constructor() {
      var _a13;
      super(...arguments);
      this.unitFormatter = (_a13 = this.props.unitFormatter) != null ? _a13 : _a5.DEFAULT_UNIT_FORMATTER;
      this.unitTextBigDisplay = import_msfs_sdk.Subject.create("");
      this.unitTextSmallDisplay = import_msfs_sdk.Subject.create("");
      this.numberText = import_msfs_sdk.Subject.create("");
      this.unitTextBig = import_msfs_sdk.Subject.create("");
      this.unitTextSmall = import_msfs_sdk.Subject.create("");
    }
    /** @inheritdoc */
    onValueChanged(value2) {
      let displayUnit = this.displayUnit.get();
      if (!displayUnit || !value2.unit.canConvert(displayUnit)) {
        displayUnit = value2.unit;
      }
      const numberValue = value2.asUnit(displayUnit);
      this.updateNumberText(numberValue);
      this.updateUnitText(numberValue, displayUnit);
      if (this.props.hideDegreeSymbolWhenNan === true) {
        this.updateUnitTextVisibility(numberValue);
      }
    }
    /** @inheritdoc */
    onDisplayUnitChanged(displayUnit) {
      const value2 = this.value.get();
      if (!displayUnit || !value2.unit.canConvert(displayUnit)) {
        displayUnit = value2.unit;
      }
      const numberValue = value2.asUnit(displayUnit);
      this.updateNumberText(numberValue);
      this.updateUnitText(numberValue, displayUnit);
      this.updateUnitTextVisibility(numberValue);
    }
    /**
     * Updates this component's displayed number text.
     * @param numberValue The numeric value to display.
     */
    updateNumberText(numberValue) {
      let numberText = this.props.formatter(numberValue);
      if (this.props.use360 !== false && parseFloat(numberText) === 0) {
        numberText = this.props.formatter(360);
      }
      this.numberText.set(numberText);
    }
    /**
     * Updates this component's displayed unit text.
     * @param numberValue The numeric value to display.
     * @param displayUnit The unit type in which to display the value.
     */
    updateUnitText(numberValue, displayUnit) {
      _a5.unitTextCache[0] = "";
      _a5.unitTextCache[1] = "";
      this.unitFormatter(_a5.unitTextCache, displayUnit, numberValue);
      this.unitTextBig.set(_a5.unitTextCache[0]);
      this.unitTextSmall.set(_a5.unitTextCache[1]);
    }
    /**
     * Updates whether this component's unit text spans are visible.
     * @param numberValue The numeric value displayed by this component.
     */
    updateUnitTextVisibility(numberValue) {
      if (this.props.hideDegreeSymbolWhenNan === true) {
        if (isNaN(numberValue)) {
          this.unitTextBigDisplay.set("none");
          this.unitTextSmallDisplay.set("none");
          return;
        }
      }
      this.unitTextBigDisplay.set(this.unitTextBig.get() === "" ? "none" : "");
      this.unitTextSmallDisplay.set(this.unitTextSmall.get() === "" ? "none" : "");
    }
    /** @inheritdoc */
    render() {
      var _a13;
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: (_a13 = this.props.class) != null ? _a13 : "", style: "white-space: nowrap;" }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", { class: "bearing-num" }, this.numberText), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", { class: "bearing-unit", style: { display: this.unitTextBigDisplay } }, this.unitTextBig), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", { class: "bearing-unit-small", style: { display: this.unitTextSmallDisplay } }, this.unitTextSmall));
    }
  }, __name(_a5, "_BearingDisplay"), _a5);
  _BearingDisplay.DEFAULT_UNIT_FORMATTER = (out, unit) => {
    out[0] = "\xB0";
    out[1] = unit.isMagnetic() ? "" : "T";
  };
  _BearingDisplay.unitTextCache = ["", ""];
  var _Input = class _Input extends GamepadUiComponent {
    constructor() {
      var _a13, _b;
      super(...arguments);
      this.uuid = import_msfs_sdk.UUID.GenerateUuid();
      this.inputRef = this.gamepadUiComponentRef;
      this.model = this.props.model || import_msfs_sdk.Subject.create(import_msfs_sdk.SubscribableUtils.toSubscribable(this.props.value || "", true).get());
      this.internalInputManager = new InternalInputManager(InternalInputActions);
      this.dispatchFocusOutEvent = this._dispatchFocusOutEvent.bind(this);
      this.setValueFromOS = this._setValueFromOS.bind(this);
      this.onKeyDown = this._onKeyDown.bind(this);
      this.onKeyUp = this._onKeyUp.bind(this);
      this.onKeyPress = this._onKeyPress.bind(this);
      this.onInput = this._onInput.bind(this);
      this.align = this.props.align || "left";
      this.debounce = new import_msfs_sdk.DebounceTimer();
      this.reloadLocalisation = this._reloadLocalisation.bind(this);
      this._isFocused = import_msfs_sdk.Subject.create(false);
      this.isFocused = this._isFocused;
      this.focusSubscription = this._isFocused.sub((focus) => {
        if (focus) {
          this.onFocusIn();
        } else {
          this.onFocusOut();
        }
      });
      this.placeholderKey = import_msfs_sdk.SubscribableUtils.toSubscribable((_a13 = this.props.placeholder) != null ? _a13 : "", true);
      this.placeholderShown = import_msfs_sdk.Subject.create(true);
      this.placeholderTranslation = import_msfs_sdk.Subject.create(this.placeholderKey.get());
      this.hidePlaceholderOnFocus = (_b = this.props.hidePlaceholderOnFocus) != null ? _b : false;
      this.subs = [];
    }
    _reloadLocalisation() {
      this.placeholderTranslation.notify();
    }
    _onKeyDown(event) {
      var _a13, _b;
      (_b = (_a13 = this.props).onKeyDown) == null ? void 0 : _b.call(_a13, event);
    }
    _onKeyUp(event) {
      var _a13, _b;
      (_b = (_a13 = this.props).onKeyUp) == null ? void 0 : _b.call(_a13, event);
    }
    _onKeyPress(event) {
      var _a13, _b;
      const keyCode = event.keyCode || event.which;
      (_b = (_a13 = this.props).onKeyPress) == null ? void 0 : _b.call(_a13, event);
      if (event.defaultPrevented) {
        return;
      }
      if (this.props.charFilter && !this.props.charFilter(String.fromCharCode(keyCode))) {
        event.preventDefault();
        return;
      }
    }
    _onInput() {
      var _a13;
      this.debounce.schedule(() => {
        const value2 = this.inputRef.instance.value;
        if (value2 === this.model.get()) {
          return;
        }
        this.model.set(value2);
      }, (_a13 = this.props.debounceDuration) != null ? _a13 : 0);
    }
    onInputUpdated(value2) {
      var _a13, _b;
      this.inputRef.instance.value = value2;
      (_b = (_a13 = this.props).onInput) == null ? void 0 : _b.call(_a13, this.inputRef.instance);
      if (!this.hidePlaceholderOnFocus && value2.length === 0) {
        this.placeholderShown.set(true);
      }
    }
    onFocusIn() {
      var _a13, _b;
      Coherent.trigger("FOCUS_INPUT_FIELD", {
        // TODO Allow the following parameters to be customized via props
        title: "Title",
        description: "",
        default_text: "",
        is_numeric: this.props.type === "number",
        is_efb: true
      });
      Coherent.on("mousePressOutsideView", this.dispatchFocusOutEvent);
      Coherent.on("SetInputTextFromOS", this.setValueFromOS);
      this.focusOutActionDestructor = this.internalInputManager.addInputAction(
        InternalInputAction.BACK,
        () => {
          this.dispatchFocusOutEvent();
          return true;
        }
      );
      this.validateInputActionDestructor = this.internalInputManager.addInputAction(
        InternalInputAction.TEXTBOX_VALID,
        () => {
          var _a22, _b2;
          (_b2 = (_a22 = this.props).onValueValidated) == null ? void 0 : _b2.call(_a22, this.inputRef.instance.value);
          this.dispatchFocusOutEvent();
          return true;
        }
      );
      if (this.hidePlaceholderOnFocus && this.inputRef.instance.value.length === 0) {
        this.placeholderShown.set(false);
      }
      (_b = (_a13 = this.props).onFocusIn) == null ? void 0 : _b.call(_a13);
    }
    onFocusOut() {
      var _a13, _b, _c, _d;
      (_a13 = this.focusOutActionDestructor) == null ? void 0 : _a13.call(this);
      (_b = this.validateInputActionDestructor) == null ? void 0 : _b.call(this);
      Coherent.trigger("UNFOCUS_INPUT_FIELD", this.uuid);
      Coherent.off("mousePressOutsideView", this.dispatchFocusOutEvent);
      Coherent.off("SetInputTextFromOS", this.setValueFromOS);
      if (this.hidePlaceholderOnFocus && this.inputRef.instance.value.length === 0) {
        this.placeholderShown.set(true);
      }
      (_d = (_c = this.props).onFocusOut) == null ? void 0 : _d.call(_c);
    }
    value() {
      return this.model.get();
    }
    clearInput() {
      this.model.set("");
    }
    focus() {
      const inputInstance = this.inputRef.getOrDefault();
      if (inputInstance !== null) {
        inputInstance.focus();
      }
    }
    blur() {
      const inputInstance = this.inputRef.getOrDefault();
      if (inputInstance !== null) {
        inputInstance.blur();
      }
    }
    _dispatchFocusOutEvent() {
      const inputInstance = this.inputRef.getOrDefault();
      if (inputInstance !== null) {
        inputInstance.blur();
        inputInstance.dispatchEvent(new InputEvent("focusout"));
      }
    }
    _setValueFromOS(text) {
      var _a13, _b;
      const inputInstance = this.inputRef.getOrDefault();
      if (inputInstance !== null) {
        inputInstance.value = text;
        inputInstance.dispatchEvent(new InputEvent("change"));
        inputInstance.dispatchEvent(new InputEvent("input"));
        inputInstance.dispatchEvent(new InputEvent("submit"));
        (_b = (_a13 = this.props).onValueValidated) == null ? void 0 : _b.call(_a13, text);
        this.dispatchFocusOutEvent();
      }
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "input",
        {
          id: this.uuid,
          ref: this.inputRef,
          type: this.props.type,
          class: {
            [`align-${this.align}`]: this.align !== "left"
          },
          placeholder: import_msfs_sdk.MappedSubject.create(
            ([placeholderShown, placeholderKey]) => {
              return placeholderShown ? Utils.Translate(placeholderKey) : "";
            },
            this.placeholderShown,
            this.placeholderKey
          ),
          disabled: this.props.disabled,
          value: import_msfs_sdk.SubscribableUtils.toSubscribable(this.props.model || this.props.value || "", true).get()
        }
      );
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.subs.push(
        this.model.sub((value2) => {
          this.onInputUpdated(value2);
        }, true),
        this.placeholderKey.sub((key) => {
          this.placeholderTranslation.set(key);
        }, true)
      );
      this.inputRef.instance.addEventListener("focus", () => this._isFocused.set(true));
      this.inputRef.instance.addEventListener("focusout", () => this._isFocused.set(false));
      this.inputRef.instance.addEventListener("input", this.onInput);
      this.inputRef.instance.addEventListener("keydown", this.onKeyDown);
      this.inputRef.instance.addEventListener("keyup", this.onKeyUp);
      this.inputRef.instance.addEventListener("keypress", this.onKeyPress);
      Coherent.on("RELOAD_LOCALISATION", this.reloadLocalisation);
      if (this.props.focusOnInit) {
        this.focus();
      }
    }
    destroy() {
      var _a13;
      this.subs.forEach((s) => s.destroy());
      this.focusSubscription.destroy();
      (_a13 = this.focusOutActionDestructor) == null ? void 0 : _a13.call(this);
      if (this._isFocused.get()) {
        Coherent.trigger("UNFOCUS_INPUT_FIELD", this.uuid);
        Coherent.off("mousePressOutsideView", this.dispatchFocusOutEvent);
        Coherent.off("SetInputTextFromOS", this.setValueFromOS);
      }
      this.inputRef.instance.removeEventListener("keydown", this.onKeyDown);
      this.inputRef.instance.removeEventListener("keyup", this.onKeyUp);
      this.inputRef.instance.removeEventListener("keypress", this.onKeyPress);
      this.inputRef.instance.removeEventListener("input", this.onInput);
      Coherent.off("RELOAD_LOCALISATION", this.reloadLocalisation);
      super.destroy();
    }
  };
  __name(_Input, "Input");
  var Input = _Input;
  var addIconPath = "coui://html_ui/efb_ui/efb_os/Assets/icons/NoMargin/Close.svg";
  var _TextBox = class _TextBox extends GamepadUiComponent {
    constructor() {
      var _a13, _b;
      super(...arguments);
      this.subscriptions = [];
      this.inputRef = import_msfs_sdk.FSComponent.createRef();
      this.model = this.props.model || import_msfs_sdk.Subject.create(import_msfs_sdk.SubscribableUtils.toSubscribable(this.props.value || "", true).get());
      this.hideDeleteTextButton = import_msfs_sdk.Subject.create(true);
      this.onDelete = this._onDelete.bind(this);
      this.onmousedown = (e) => {
        e.preventDefault();
        if (!this.inputRef.instance.isFocused.get()) {
          this.inputRef.instance.focus();
        }
      };
      this.prefix = import_msfs_sdk.SubscribableUtils.toSubscribable(
        this.props.prefix || "",
        true
      );
      this.suffix = import_msfs_sdk.SubscribableUtils.toSubscribable(
        this.props.suffix || "",
        true
      );
      this.showPrefix = import_msfs_sdk.SubscribableUtils.toSubscribable((_a13 = this.props.showPrefix) != null ? _a13 : true, true);
      this.showSuffix = import_msfs_sdk.SubscribableUtils.toSubscribable((_b = this.props.showSuffix) != null ? _b : true, true);
      this.prefixRef = import_msfs_sdk.FSComponent.createRef();
      this.suffixRef = import_msfs_sdk.FSComponent.createRef();
      this.showPrefixOrSuffixMapFunction = ([value2, show]) => {
        return !show || value2.toString().length === 0;
      };
    }
    _onDelete(event) {
      var _a13, _b;
      event.preventDefault();
      this.hideDeleteTextButton.set(true);
      this.inputRef.instance.clearInput();
      (_b = (_a13 = this.props).onDelete) == null ? void 0 : _b.call(_a13);
      if (this.props.focusOnClear) {
        this.inputRef.instance.focus();
      }
    }
    onInput(input) {
      var _a13, _b;
      (_b = (_a13 = this.props).onInput) == null ? void 0 : _b.call(_a13, input);
      if (this.props.showDeleteIcon) {
        this.hideDeleteTextButton.set(input.value.trim().length === 0);
      }
    }
    onFocusIn() {
      var _a13, _b;
      (_b = (_a13 = this.props).onFocusIn) == null ? void 0 : _b.call(_a13);
      this.gamepadUiComponentRef.instance.classList.add("textbox--focused");
    }
    onFocusOut() {
      var _a13, _b;
      (_b = (_a13 = this.props).onFocusOut) == null ? void 0 : _b.call(_a13);
      this.gamepadUiComponentRef.instance.classList.remove("textbox--focused");
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { ref: this.gamepadUiComponentRef, class: "textbox" }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "disabled-layer" }), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "div",
        {
          ref: this.prefixRef,
          class: {
            prefix: true,
            "prefix--hidden": import_msfs_sdk.MappedSubject.create(this.prefix, this.showPrefix).map(
              this.showPrefixOrSuffixMapFunction
            )
          }
        }
      ), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        Input,
        {
          ref: this.inputRef,
          type: "text",
          align: this.props.align,
          model: this.model,
          placeholder: this.props.placeholder,
          disabled: this.props.disabled,
          hidePlaceholderOnFocus: this.props.hidePlaceholderOnFocus,
          focusOnInit: this.props.focusOnInit,
          onFocusIn: this.onFocusIn.bind(this),
          onFocusOut: this.onFocusOut.bind(this),
          onInput: this.onInput.bind(this),
          onValueValidated: this.props.onValueValidated,
          debounceDuration: this.props.debounceDuration,
          onKeyDown: this.props.onKeyDown,
          onKeyUp: this.props.onKeyUp,
          onKeyPress: this.props.onKeyPress,
          charFilter: this.props.charFilter
        }
      ), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "div",
        {
          ref: this.suffixRef,
          class: {
            suffix: true,
            "suffix--hidden": import_msfs_sdk.MappedSubject.create(this.suffix, this.showSuffix).map(
              this.showPrefixOrSuffixMapFunction
            )
          }
        }
      ), this.props.showDeleteIcon && /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        IconButton,
        {
          class: mergeClassProp("delete-text-button", { hide: this.hideDeleteTextButton }),
          iconPath: addIconPath,
          callback: this.onDelete
        }
      ));
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      const prefixSuffixCb = /* @__PURE__ */ __name((reference) => {
        let previousNode = null;
        return (value2) => {
          if (previousNode) {
            import_msfs_sdk.FSComponent.shallowDestroy(previousNode);
            const instance = previousNode.instance;
            if (instance && instance instanceof import_msfs_sdk.DisplayComponent) {
              instance.destroy();
            }
            previousNode = null;
          }
          if (typeof value2 === "string") {
            reference.instance.innerText = value2;
          } else if (typeof value2 === "object" && isVNode(value2)) {
            previousNode = value2;
            reference.instance.innerHTML = "";
            import_msfs_sdk.FSComponent.render(value2, reference.instance);
          } else {
            reference.instance.innerText = value2;
          }
        };
      }, "prefixSuffixCb");
      this.subscriptions.push(
        this.prefix.sub(prefixSuffixCb(this.prefixRef), true),
        this.suffix.sub(prefixSuffixCb(this.suffixRef), true)
      );
      this.gamepadUiComponentRef.instance.addEventListener("mousedown", this.onmousedown);
    }
    destroy() {
      this.gamepadUiComponentRef.instance.removeEventListener("mousedown", this.onmousedown);
      this.subscriptions.forEach((s) => s.destroy());
      super.destroy();
    }
  };
  __name(_TextBox, "TextBox");
  var TextBox = _TextBox;
  var _a6;
  var _UnitBox = (_a6 = class extends GamepadUiComponent {
    constructor() {
      var _a13, _b;
      super(...arguments);
      this.valueNumber = this.props.value;
      this.unit = import_msfs_sdk.SubscribableUtils.toSubscribable((_a13 = this.props.unit) != null ? _a13 : null, true);
      this.showUnitSuffix = import_msfs_sdk.SubscribableUtils.toSubscribable((_b = this.props.showSuffix) != null ? _b : true, true);
      this.textValue = import_msfs_sdk.Subject.create("0");
      this.formattedTextValue = import_msfs_sdk.Subject.create("0");
      this.subs = [];
      this.canUpdateDisplay = true;
      this.suffixSmall = import_msfs_sdk.Subject.create("");
      this.suffixBig = import_msfs_sdk.Subject.create("");
    }
    static createDefaultUnitTextMap() {
      const originalMap = UnitFormatter.getUnitTextMap();
      const map = {};
      for (const family in originalMap) {
        const nameMap = map[family] = {};
        const originalNameMap = originalMap[family];
        for (const name in originalNameMap) {
          const text = nameMap[name] = ["", ""];
          const originalText = originalNameMap[name];
          if (originalText[0].startsWith("\xB0")) {
            text[0] = "\xB0";
            text[1] = originalText.substring(1);
          } else {
            text[1] = originalText;
          }
        }
      }
      return map;
    }
    setValue(value2, displayUnit) {
      var _a13;
      if (!displayUnit || !value2.unit.canConvert(displayUnit)) {
        displayUnit = value2.unit;
      }
      let numberValue = value2.asUnit(displayUnit);
      if (this.props.min && numberValue < this.props.min.asUnit(displayUnit)) {
        numberValue = this.props.min.asUnit(displayUnit);
      }
      if (this.props.max && numberValue > this.props.max.asUnit(displayUnit)) {
        numberValue = this.props.max.asUnit(displayUnit);
      }
      if (numberValue >= 9999999) {
        this.textValue.set("9999999");
        return;
      }
      const numberText = import_msfs_sdk.NumberFormatter.create(__spreadValues2({
        nanString: "0"
      }, (_a13 = this.props.numberFormatterOptions) != null ? _a13 : {}))(numberValue);
      if (numberText.includes(".")) {
        const dotPos = numberText.indexOf(".");
        this.textValue.set(numberText.substring(0, dotPos + 3));
      } else {
        this.textValue.set(this.textValue.get().includes(".") ? `${numberText}.` : numberText);
      }
      const unitTexts = _a6.DEFAULT_UNIT_FORMATTER(displayUnit);
      if (unitTexts === null) {
        this.suffixSmall.set("");
        this.suffixBig.set("");
        return;
      }
      this.suffixSmall.set(unitTexts[0]);
      this.suffixBig.set(unitTexts[1]);
    }
    updateDisplay() {
      var _a13, _b;
      (_b = (_a13 = this.props).onFocusOut) == null ? void 0 : _b.call(_a13);
      if (this.canUpdateDisplay) {
        this.formattedTextValue.set(this.textValue.get());
      }
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "div",
        {
          ref: this.gamepadUiComponentRef,
          class: { unitbox: true, "unitbox--disabled": this.props.disabled || false }
        },
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
          TextBox,
          {
            align: "right",
            showDeleteIcon: this.props.showDeleteIcon || false,
            customDeleteIcon: this.props.customDeleteIcon,
            focusOnClear: this.props.focusOnClear,
            onDelete: this.props.onDelete,
            model: this.formattedTextValue,
            placeholder: this.props.placeholder,
            hidePlaceholderOnFocus: this.props.hidePlaceholderOnFocus,
            disabled: this.props.disabled,
            focusOnInit: this.props.focusOnInit,
            onFocusIn: this.props.onFocusIn,
            onFocusOut: this.updateDisplay.bind(this),
            onInput: this.props.onInput,
            onKeyPress: this.props.onKeyPress,
            charFilter: /* @__PURE__ */ __name((char) => char >= "0" && char <= "9" || [",", ".", "-"].includes(char), "charFilter"),
            suffix: /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(import_msfs_sdk.FSComponent.Fragment, null, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", null, this.suffixSmall), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", null, this.suffixBig)),
            showSuffix: this.showUnitSuffix
          }
        )
      );
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.subs.push(
        this.formattedTextValue.sub((value2) => {
          value2 = value2.replace(",", ".").replace(/\.+$/, "");
          let displayUnit = this.unit.get();
          if (!displayUnit || !this.valueNumber.get().unit.canConvert(displayUnit)) {
            displayUnit = this.valueNumber.get().unit;
          }
          const unitValue = displayUnit.convertTo(Number(value2), this.valueNumber.get().unit);
          this.canUpdateDisplay = false;
          this.valueNumber.set(unitValue);
          this.canUpdateDisplay = true;
        }),
        this.valueNumber.sub((value2) => {
          this.setValue(value2, this.unit.get());
          this.updateDisplay();
        }, true),
        this.unit.sub((unit) => {
          this.setValue(this.valueNumber.get(), unit);
          this.updateDisplay();
        }, true)
      );
    }
    destroy() {
      this.subs.forEach((s) => s.destroy());
      super.destroy();
    }
  }, __name(_a6, "_UnitBox"), _a6);
  _UnitBox.DEFAULT_UNIT_TEXT_MAP = _UnitBox.createDefaultUnitTextMap();
  _UnitBox.DEFAULT_UNIT_FORMATTER = (unit) => {
    var _a13;
    const text = (_a13 = _UnitBox.DEFAULT_UNIT_TEXT_MAP[unit.family]) == null ? void 0 : _a13[unit.name];
    if (text) {
      return text;
    }
    return null;
  };
  var MapShim = function() {
    if (typeof Map !== "undefined") {
      return Map;
    }
    function getIndex(arr, key) {
      var result = -1;
      arr.some(function(entry, index2) {
        if (entry[0] === key) {
          result = index2;
          return true;
        }
        return false;
      });
      return result;
    }
    __name(getIndex, "getIndex");
    return (
      /** @class */
      function() {
        function class_1() {
          this.__entries__ = [];
        }
        __name(class_1, "class_1");
        Object.defineProperty(class_1.prototype, "size", {
          /**
           * @returns {boolean}
           */
          get: /* @__PURE__ */ __name(function() {
            return this.__entries__.length;
          }, "get"),
          enumerable: true,
          configurable: true
        });
        class_1.prototype.get = function(key) {
          var index2 = getIndex(this.__entries__, key);
          var entry = this.__entries__[index2];
          return entry && entry[1];
        };
        class_1.prototype.set = function(key, value2) {
          var index2 = getIndex(this.__entries__, key);
          if (~index2) {
            this.__entries__[index2][1] = value2;
          } else {
            this.__entries__.push([key, value2]);
          }
        };
        class_1.prototype.delete = function(key) {
          var entries = this.__entries__;
          var index2 = getIndex(entries, key);
          if (~index2) {
            entries.splice(index2, 1);
          }
        };
        class_1.prototype.has = function(key) {
          return !!~getIndex(this.__entries__, key);
        };
        class_1.prototype.clear = function() {
          this.__entries__.splice(0);
        };
        class_1.prototype.forEach = function(callback, ctx) {
          if (ctx === void 0) {
            ctx = null;
          }
          for (var _i = 0, _a13 = this.__entries__; _i < _a13.length; _i++) {
            var entry = _a13[_i];
            callback.call(ctx, entry[1], entry[0]);
          }
        };
        return class_1;
      }()
    );
  }();
  var isBrowser = typeof window !== "undefined" && typeof document !== "undefined" && window.document === document;
  var global$1 = function() {
    if (typeof global !== "undefined" && global.Math === Math) {
      return global;
    }
    if (typeof self !== "undefined" && self.Math === Math) {
      return self;
    }
    if (typeof window !== "undefined" && window.Math === Math) {
      return window;
    }
    return Function("return this")();
  }();
  var requestAnimationFrame$1 = function() {
    if (typeof requestAnimationFrame === "function") {
      return requestAnimationFrame.bind(global$1);
    }
    return function(callback) {
      return setTimeout(function() {
        return callback(Date.now());
      }, 1e3 / 60);
    };
  }();
  var trailingTimeout = 2;
  function throttle(callback, delay) {
    var leadingCall = false, trailingCall = false, lastCallTime = 0;
    function resolvePending() {
      if (leadingCall) {
        leadingCall = false;
        callback();
      }
      if (trailingCall) {
        proxy();
      }
    }
    __name(resolvePending, "resolvePending");
    function timeoutCallback() {
      requestAnimationFrame$1(resolvePending);
    }
    __name(timeoutCallback, "timeoutCallback");
    function proxy() {
      var timeStamp = Date.now();
      if (leadingCall) {
        if (timeStamp - lastCallTime < trailingTimeout) {
          return;
        }
        trailingCall = true;
      } else {
        leadingCall = true;
        trailingCall = false;
        setTimeout(timeoutCallback, delay);
      }
      lastCallTime = timeStamp;
    }
    __name(proxy, "proxy");
    return proxy;
  }
  __name(throttle, "throttle");
  var REFRESH_DELAY = 20;
  var transitionKeys = ["top", "right", "bottom", "left", "width", "height", "size", "weight"];
  var mutationObserverSupported = typeof MutationObserver !== "undefined";
  var ResizeObserverController = (
    /** @class */
    function() {
      function ResizeObserverController2() {
        this.connected_ = false;
        this.mutationEventsAdded_ = false;
        this.mutationsObserver_ = null;
        this.observers_ = [];
        this.onTransitionEnd_ = this.onTransitionEnd_.bind(this);
        this.refresh = throttle(this.refresh.bind(this), REFRESH_DELAY);
      }
      __name(ResizeObserverController2, "ResizeObserverController2");
      ResizeObserverController2.prototype.addObserver = function(observer) {
        if (!~this.observers_.indexOf(observer)) {
          this.observers_.push(observer);
        }
        if (!this.connected_) {
          this.connect_();
        }
      };
      ResizeObserverController2.prototype.removeObserver = function(observer) {
        var observers2 = this.observers_;
        var index2 = observers2.indexOf(observer);
        if (~index2) {
          observers2.splice(index2, 1);
        }
        if (!observers2.length && this.connected_) {
          this.disconnect_();
        }
      };
      ResizeObserverController2.prototype.refresh = function() {
        var changesDetected = this.updateObservers_();
        if (changesDetected) {
          this.refresh();
        }
      };
      ResizeObserverController2.prototype.updateObservers_ = function() {
        var activeObservers = this.observers_.filter(function(observer) {
          return observer.gatherActive(), observer.hasActive();
        });
        activeObservers.forEach(function(observer) {
          return observer.broadcastActive();
        });
        return activeObservers.length > 0;
      };
      ResizeObserverController2.prototype.connect_ = function() {
        if (!isBrowser || this.connected_) {
          return;
        }
        document.addEventListener("transitionend", this.onTransitionEnd_);
        window.addEventListener("resize", this.refresh);
        if (mutationObserverSupported) {
          this.mutationsObserver_ = new MutationObserver(this.refresh);
          this.mutationsObserver_.observe(document, {
            attributes: true,
            childList: true,
            characterData: true,
            subtree: true
          });
        } else {
          document.addEventListener("DOMSubtreeModified", this.refresh);
          this.mutationEventsAdded_ = true;
        }
        this.connected_ = true;
      };
      ResizeObserverController2.prototype.disconnect_ = function() {
        if (!isBrowser || !this.connected_) {
          return;
        }
        document.removeEventListener("transitionend", this.onTransitionEnd_);
        window.removeEventListener("resize", this.refresh);
        if (this.mutationsObserver_) {
          this.mutationsObserver_.disconnect();
        }
        if (this.mutationEventsAdded_) {
          document.removeEventListener("DOMSubtreeModified", this.refresh);
        }
        this.mutationsObserver_ = null;
        this.mutationEventsAdded_ = false;
        this.connected_ = false;
      };
      ResizeObserverController2.prototype.onTransitionEnd_ = function(_a13) {
        var _b = _a13.propertyName, propertyName = _b === void 0 ? "" : _b;
        var isReflowProperty = transitionKeys.some(function(key) {
          return !!~propertyName.indexOf(key);
        });
        if (isReflowProperty) {
          this.refresh();
        }
      };
      ResizeObserverController2.getInstance = function() {
        if (!this.instance_) {
          this.instance_ = new ResizeObserverController2();
        }
        return this.instance_;
      };
      ResizeObserverController2.instance_ = null;
      return ResizeObserverController2;
    }()
  );
  var defineConfigurable = /* @__PURE__ */ __name(function(target, props) {
    for (var _i = 0, _a13 = Object.keys(props); _i < _a13.length; _i++) {
      var key = _a13[_i];
      Object.defineProperty(target, key, {
        value: props[key],
        enumerable: false,
        writable: false,
        configurable: true
      });
    }
    return target;
  }, "defineConfigurable");
  var getWindowOf = /* @__PURE__ */ __name(function(target) {
    var ownerGlobal = target && target.ownerDocument && target.ownerDocument.defaultView;
    return ownerGlobal || global$1;
  }, "getWindowOf");
  var emptyRect = createRectInit(0, 0, 0, 0);
  function toFloat(value2) {
    return parseFloat(value2) || 0;
  }
  __name(toFloat, "toFloat");
  function getBordersSize(styles) {
    var positions = [];
    for (var _i = 1; _i < arguments.length; _i++) {
      positions[_i - 1] = arguments[_i];
    }
    return positions.reduce(function(size, position) {
      var value2 = styles["border-" + position + "-width"];
      return size + toFloat(value2);
    }, 0);
  }
  __name(getBordersSize, "getBordersSize");
  function getPaddings(styles) {
    var positions = ["top", "right", "bottom", "left"];
    var paddings = {};
    for (var _i = 0, positions_1 = positions; _i < positions_1.length; _i++) {
      var position = positions_1[_i];
      var value2 = styles["padding-" + position];
      paddings[position] = toFloat(value2);
    }
    return paddings;
  }
  __name(getPaddings, "getPaddings");
  function getSVGContentRect(target) {
    var bbox = target.getBBox();
    return createRectInit(0, 0, bbox.width, bbox.height);
  }
  __name(getSVGContentRect, "getSVGContentRect");
  function getHTMLElementContentRect(target) {
    var clientWidth = target.clientWidth, clientHeight = target.clientHeight;
    if (!clientWidth && !clientHeight) {
      return emptyRect;
    }
    var styles = getWindowOf(target).getComputedStyle(target);
    var paddings = getPaddings(styles);
    var horizPad = paddings.left + paddings.right;
    var vertPad = paddings.top + paddings.bottom;
    var width = toFloat(styles.width), height = toFloat(styles.height);
    if (styles.boxSizing === "border-box") {
      if (Math.round(width + horizPad) !== clientWidth) {
        width -= getBordersSize(styles, "left", "right") + horizPad;
      }
      if (Math.round(height + vertPad) !== clientHeight) {
        height -= getBordersSize(styles, "top", "bottom") + vertPad;
      }
    }
    if (!isDocumentElement(target)) {
      var vertScrollbar = Math.round(width + horizPad) - clientWidth;
      var horizScrollbar = Math.round(height + vertPad) - clientHeight;
      if (Math.abs(vertScrollbar) !== 1) {
        width -= vertScrollbar;
      }
      if (Math.abs(horizScrollbar) !== 1) {
        height -= horizScrollbar;
      }
    }
    return createRectInit(paddings.left, paddings.top, width, height);
  }
  __name(getHTMLElementContentRect, "getHTMLElementContentRect");
  var isSVGGraphicsElement = function() {
    if (typeof SVGGraphicsElement !== "undefined") {
      return function(target) {
        return target instanceof getWindowOf(target).SVGGraphicsElement;
      };
    }
    return function(target) {
      return target instanceof getWindowOf(target).SVGElement && typeof target.getBBox === "function";
    };
  }();
  function isDocumentElement(target) {
    return target === getWindowOf(target).document.documentElement;
  }
  __name(isDocumentElement, "isDocumentElement");
  function getContentRect(target) {
    if (!isBrowser) {
      return emptyRect;
    }
    if (isSVGGraphicsElement(target)) {
      return getSVGContentRect(target);
    }
    return getHTMLElementContentRect(target);
  }
  __name(getContentRect, "getContentRect");
  function createReadOnlyRect(_a13) {
    var x = _a13.x, y = _a13.y, width = _a13.width, height = _a13.height;
    var Constr = typeof DOMRectReadOnly !== "undefined" ? DOMRectReadOnly : Object;
    var rect = Object.create(Constr.prototype);
    defineConfigurable(rect, {
      x,
      y,
      width,
      height,
      top: y,
      right: x + width,
      bottom: height + y,
      left: x
    });
    return rect;
  }
  __name(createReadOnlyRect, "createReadOnlyRect");
  function createRectInit(x, y, width, height) {
    return { x, y, width, height };
  }
  __name(createRectInit, "createRectInit");
  var ResizeObservation = (
    /** @class */
    function() {
      function ResizeObservation2(target) {
        this.broadcastWidth = 0;
        this.broadcastHeight = 0;
        this.contentRect_ = createRectInit(0, 0, 0, 0);
        this.target = target;
      }
      __name(ResizeObservation2, "ResizeObservation2");
      ResizeObservation2.prototype.isActive = function() {
        var rect = getContentRect(this.target);
        this.contentRect_ = rect;
        return rect.width !== this.broadcastWidth || rect.height !== this.broadcastHeight;
      };
      ResizeObservation2.prototype.broadcastRect = function() {
        var rect = this.contentRect_;
        this.broadcastWidth = rect.width;
        this.broadcastHeight = rect.height;
        return rect;
      };
      return ResizeObservation2;
    }()
  );
  var ResizeObserverEntry = (
    /** @class */
    /* @__PURE__ */ function() {
      function ResizeObserverEntry2(target, rectInit) {
        var contentRect = createReadOnlyRect(rectInit);
        defineConfigurable(this, { target, contentRect });
      }
      __name(ResizeObserverEntry2, "ResizeObserverEntry2");
      return ResizeObserverEntry2;
    }()
  );
  var ResizeObserverSPI = (
    /** @class */
    function() {
      function ResizeObserverSPI2(callback, controller, callbackCtx) {
        this.activeObservations_ = [];
        this.observations_ = new MapShim();
        if (typeof callback !== "function") {
          throw new TypeError("The callback provided as parameter 1 is not a function.");
        }
        this.callback_ = callback;
        this.controller_ = controller;
        this.callbackCtx_ = callbackCtx;
      }
      __name(ResizeObserverSPI2, "ResizeObserverSPI2");
      ResizeObserverSPI2.prototype.observe = function(target) {
        if (!arguments.length) {
          throw new TypeError("1 argument required, but only 0 present.");
        }
        if (typeof Element === "undefined" || !(Element instanceof Object)) {
          return;
        }
        if (!(target instanceof getWindowOf(target).Element)) {
          throw new TypeError('parameter 1 is not of type "Element".');
        }
        var observations = this.observations_;
        if (observations.has(target)) {
          return;
        }
        observations.set(target, new ResizeObservation(target));
        this.controller_.addObserver(this);
        this.controller_.refresh();
      };
      ResizeObserverSPI2.prototype.unobserve = function(target) {
        if (!arguments.length) {
          throw new TypeError("1 argument required, but only 0 present.");
        }
        if (typeof Element === "undefined" || !(Element instanceof Object)) {
          return;
        }
        if (!(target instanceof getWindowOf(target).Element)) {
          throw new TypeError('parameter 1 is not of type "Element".');
        }
        var observations = this.observations_;
        if (!observations.has(target)) {
          return;
        }
        observations.delete(target);
        if (!observations.size) {
          this.controller_.removeObserver(this);
        }
      };
      ResizeObserverSPI2.prototype.disconnect = function() {
        this.clearActive();
        this.observations_.clear();
        this.controller_.removeObserver(this);
      };
      ResizeObserverSPI2.prototype.gatherActive = function() {
        var _this = this;
        this.clearActive();
        this.observations_.forEach(function(observation) {
          if (observation.isActive()) {
            _this.activeObservations_.push(observation);
          }
        });
      };
      ResizeObserverSPI2.prototype.broadcastActive = function() {
        if (!this.hasActive()) {
          return;
        }
        var ctx = this.callbackCtx_;
        var entries = this.activeObservations_.map(function(observation) {
          return new ResizeObserverEntry(observation.target, observation.broadcastRect());
        });
        this.callback_.call(ctx, entries, ctx);
        this.clearActive();
      };
      ResizeObserverSPI2.prototype.clearActive = function() {
        this.activeObservations_.splice(0);
      };
      ResizeObserverSPI2.prototype.hasActive = function() {
        return this.activeObservations_.length > 0;
      };
      return ResizeObserverSPI2;
    }()
  );
  var observers = typeof WeakMap !== "undefined" ? /* @__PURE__ */ new WeakMap() : new MapShim();
  var ResizeObserver = (
    /** @class */
    /* @__PURE__ */ function() {
      function ResizeObserver2(callback) {
        if (!(this instanceof ResizeObserver2)) {
          throw new TypeError("Cannot call a class as a function.");
        }
        if (!arguments.length) {
          throw new TypeError("1 argument required, but only 0 present.");
        }
        var controller = ResizeObserverController.getInstance();
        var observer = new ResizeObserverSPI(callback, controller, this);
        observers.set(this, observer);
      }
      __name(ResizeObserver2, "ResizeObserver2");
      return ResizeObserver2;
    }()
  );
  [
    "observe",
    "unobserve",
    "disconnect"
  ].forEach(function(method) {
    ResizeObserver.prototype[method] = function() {
      var _a13;
      return (_a13 = observers.get(this))[method].apply(_a13, arguments);
    };
  });
  var index = function() {
    if (typeof global$1.ResizeObserver !== "undefined") {
      return global$1.ResizeObserver;
    }
    return ResizeObserver;
  }();
  var _a7;
  var _NumberUnitDisplay = (_a7 = class extends import_msfs_sdk.AbstractNumberUnitDisplay {
    constructor() {
      var _a13, _b;
      super(...arguments);
      this.formatter = (_a13 = this.props.formatter) != null ? _a13 : import_msfs_sdk.NumberFormatter.create({
        // TODO Properly format the time
        precision: 0.01,
        maxDigits: 5,
        forceDecimalZeroes: false,
        nanString: "0"
      });
      this.unitFormatter = (_b = this.props.unitFormatter) != null ? _b : _a7.DEFAULT_UNIT_FORMATTER;
      this.unitTextBigDisplay = import_msfs_sdk.Subject.create("");
      this.unitTextSmallDisplay = import_msfs_sdk.Subject.create("");
      this.numberText = import_msfs_sdk.Subject.create("");
      this.unitTextBig = import_msfs_sdk.Subject.create("");
      this.unitTextSmall = import_msfs_sdk.Subject.create("");
    }
    /** @inheritdoc */
    onValueChanged(value2) {
      this.updateDisplay(value2, this.displayUnit.get());
    }
    /** @inheritdoc */
    onDisplayUnitChanged(displayUnit) {
      this.updateDisplay(this.value.get(), displayUnit);
    }
    /**
     * Updates this component's displayed number and unit text.
     * @param value The value to display.
     * @param displayUnit The unit type in which to display the value, or `null` if the value should be displayed in its
     * native unit type.
     */
    updateDisplay(value2, displayUnit) {
      if (!displayUnit || !value2.unit.canConvert(displayUnit)) {
        displayUnit = value2.unit;
      }
      const numberValue = value2.asUnit(displayUnit);
      const numberText = this.formatter(numberValue);
      this.numberText.set(numberText);
      _a7.unitTextCache[0] = "";
      _a7.unitTextCache[1] = "";
      this.unitFormatter(_a7.unitTextCache, displayUnit, numberValue);
      this.unitTextBig.set(_a7.unitTextCache[0]);
      this.unitTextSmall.set(_a7.unitTextCache[1]);
      this.updateUnitTextVisibility(
        numberValue,
        _a7.unitTextCache[0],
        _a7.unitTextCache[1]
      );
    }
    /**
     * Updates whether this component's unit text spans are visible.
     * @param numberValue The numeric value displayed by this component.
     * @param unitTextBig The text to display in the big text span.
     * @param unitTextSmall The text to display in the small text span.
     */
    updateUnitTextVisibility(numberValue, unitTextBig, unitTextSmall) {
      if (this.props.hideUnitWhenNaN === true && isNaN(numberValue)) {
        this.unitTextBigDisplay.set("none");
        this.unitTextSmallDisplay.set("none");
        return;
      }
      this.unitTextBigDisplay.set(unitTextBig === "" ? "none" : "");
      this.unitTextSmallDisplay.set(unitTextSmall === "" ? "none" : "");
    }
    /**
     * Creates the default mapping from unit to displayed text.
     * @returns The default mapping from unit to displayed text.
     */
    static createDefaultUnitTextMap() {
      const originalMap = UnitFormatter.getUnitTextMap();
      const map = {};
      for (const family in originalMap) {
        const nameMap = map[family] = {};
        const originalNameMap = originalMap[family];
        for (const name in originalNameMap) {
          const text = nameMap[name] = ["", ""];
          const originalText = originalNameMap[name];
          if (originalText[0] === "\xB0") {
            text[0] = "\xB0";
            text[1] = originalText.substring(1);
          } else {
            text[1] = originalText;
          }
        }
      }
      return map;
    }
    render() {
      var _a13;
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: (_a13 = this.props.class) != null ? _a13 : "", style: "white-space: nowrap;" }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", { class: "numberunit-num" }, this.numberText), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", { class: "numberunit-unit-big", style: { display: this.unitTextBigDisplay } }, this.unitTextBig), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("span", { class: "numberunit-unit-small", style: { display: this.unitTextSmallDisplay } }, this.unitTextSmall));
    }
  }, __name(_a7, "_NumberUnitDisplay"), _a7);
  _NumberUnitDisplay.DEFAULT_UNIT_TEXT_MAP = _NumberUnitDisplay.createDefaultUnitTextMap();
  _NumberUnitDisplay.DEFAULT_UNIT_FORMATTER = (out, unit) => {
    var _a13;
    const text = (_a13 = _NumberUnitDisplay.DEFAULT_UNIT_TEXT_MAP[unit.family]) == null ? void 0 : _a13[unit.name];
    if (text) {
      out[0] = text[0];
      out[1] = text[1];
    }
  };
  _NumberUnitDisplay.unitTextCache = ["", ""];
  var _a8;
  var _FacilitySearchUtils = (_a8 = class {
    constructor(bus) {
      this.facilityLoader = null;
      this.position = new import_msfs_sdk.GeoPoint(0, 0);
      bus.getSubscriber().on("gps-position").handle((pos) => {
        this.position.set(pos.lat, pos.long);
      });
      this.facilityLoader = new import_msfs_sdk.FacilityLoader(import_msfs_sdk.FacilityRepository.getRepository(bus));
    }
    static getSearchUtils(bus) {
      var _a13;
      return (_a13 = _a8.INSTANCE) != null ? _a13 : _a8.INSTANCE = new _a8(bus);
    }
    orderByIdentsAndDistance(a, b) {
      const aIdent = a.icaoStruct.ident.trim();
      const bIdent = b.icaoStruct.ident.trim();
      if (aIdent === bIdent) {
        const aDist = this.position.distance(a.lat, a.lon);
        const bDist = this.position.distance(b.lat, b.lon);
        return aDist - bDist;
      } else {
        return aIdent.localeCompare(bIdent);
      }
    }
    /**
     * Loads facilities based on an ident to search for, a search type and whether to exclude terminal facilities
     * @param ident The ident to search for
     * @param facilitySearchType The search type. Defaults to {@link FacilitySearchType.All}
     * @param excludeTerminalFacilities Whether to exclude terminal facilities. Defaults to `true`.
     * @returns a readonly array of facilities
     */
    async loadFacilities(ident, facilitySearchType = import_msfs_sdk.FacilitySearchType.All, excludeTerminalFacilities = true) {
      if (this.facilityLoader === null) {
        return Promise.resolve([]);
      }
      const icaos = await this.facilityLoader.searchByIdentWithIcaoStructs(facilitySearchType, ident, 15);
      const facilities = (await this.facilityLoader.getFacilities(icaos)).filter(
        (fac) => fac !== null && (!excludeTerminalFacilities || fac.icaoStruct.airport.length === 0)
      );
      facilities.sort((a, b) => this.orderByIdentsAndDistance(a, b));
      return facilities;
    }
  }, __name(_a8, "_FacilitySearchUtils"), _a8);
  _FacilitySearchUtils.INSTANCE = null;
  var _a9;
  var _SearchFacilityHistoryManager = (_a9 = class {
    constructor() {
      this.MAX_ITEMS_STORED = 5;
      this.storedICAOs = import_msfs_sdk.ArraySubject.create();
      this.loadICAOsFromStorage();
    }
    /**
     * Retrieves all the stored recent searches
     */
    loadICAOsFromStorage() {
      let arrayICAOs = [];
      const stringICAOs = import_msfs_sdk.DataStore.get(_a9.DATASTORE_KEY);
      if (stringICAOs === void 0 || typeof stringICAOs !== "string") {
        this.storedICAOs.set(arrayICAOs);
        return;
      }
      try {
        arrayICAOs = JSON.parse(stringICAOs);
      } catch (e) {
        console.error("JSON failed, impossible to parse : ", stringICAOs);
      }
      this.storedICAOs.set(arrayICAOs);
    }
    saveICAOsToStorage() {
      const stringICAOs = JSON.stringify(this.storedICAOs.getArray());
      import_msfs_sdk.DataStore.set(_a9.DATASTORE_KEY, stringICAOs);
    }
    mostRecentSearch(icao) {
      const foundIndex = this.storedICAOs.getArray().findIndex((storedIcao) => storedIcao === icao);
      if (foundIndex !== -1) {
        this.storedICAOs.removeAt(foundIndex);
      }
      this.storedICAOs.insert(icao, 0);
      while (this.storedICAOs.length > this.MAX_ITEMS_STORED) {
        this.storedICAOs.removeAt(this.MAX_ITEMS_STORED);
      }
      this.saveICAOsToStorage();
    }
    /**
     * Retrieve the search facility history as an array
     * @param input the input of the search
     * @param max_items the maximum number of items returned
     * @returns the recent searches as an array of icaos
     */
    getStoredICAOs(input, max_items = this.MAX_ITEMS_STORED) {
      this.loadICAOsFromStorage();
      return this.storedICAOs.getArray().filter((icao) => import_msfs_sdk.ICAO.getIdent(icao).startsWith(input.toUpperCase())).slice(0, max_items);
    }
  }, __name(_a9, "_SearchFacilityHistoryManager"), _a9);
  _SearchFacilityHistoryManager.DATASTORE_KEY = "efb.search-bar-history";
  var _Slider = class _Slider extends GamepadUiComponent {
    constructor(props) {
      var _a13, _b, _c, _d, _e, _f, _g;
      super(props);
      this.max = import_msfs_sdk.SubscribableUtils.toSubscribable((_a13 = this.props.max) != null ? _a13 : 100, true);
      this.min = import_msfs_sdk.SubscribableUtils.toSubscribable((_b = this.props.min) != null ? _b : 0, true);
      this.isButtonDisabled = import_msfs_sdk.SubscribableUtils.toSubscribable((_c = this.props.disabled) != null ? _c : false, true);
      this.isButtonSelected = import_msfs_sdk.Subject.create(false);
      this.precision = import_msfs_sdk.MappedSubject.create(
        ([step, min, _max]) => this.convertValueToPercent(step + min),
        import_msfs_sdk.SubscribableUtils.toSubscribable((_d = this.props.step) != null ? _d : 0, true),
        this.min,
        this.max
      );
      this.isButtonHoverable = import_msfs_sdk.MappedSubject.create(
        ([isHoverable, isDisabled, isSelected]) => {
          return isHoverable && !isDisabled && !isSelected;
        },
        import_msfs_sdk.SubscribableUtils.toSubscribable((_e = this.props.hoverable) != null ? _e : true, true),
        this.isButtonDisabled,
        this.isButtonSelected
      );
      this.verticalSlider = (_f = this.props.vertical) != null ? _f : false;
      this.allowWheel = (_g = this.props.allowWheel) != null ? _g : false;
      this.sliderBarRef = import_msfs_sdk.FSComponent.createRef();
      this.sliderBarRect = new DOMRect();
      this.allowMovement = false;
      this.mousePos = 0;
      this.subs = [];
      this.onGlobalMouseUp = this._onGlobalMouseUp.bind(this);
      this.onGlobalMouseMove = this._onGlobalMouseMove.bind(this);
      if (import_msfs_sdk.SubscribableUtils.isMutableSubscribable(props.value)) {
        this.value = props.value;
      } else if (import_msfs_sdk.SubscribableUtils.isSubscribable(props.value)) {
        this.value = import_msfs_sdk.Subject.create(props.value.get());
        this.subscribableValueSubscription = props.value.sub((value2) => this.value.set(value2));
      } else {
        this.value = import_msfs_sdk.Subject.create(props.value);
      }
      this.valuePercent = import_msfs_sdk.Subject.create(this.convertValueToPercent(this.value.get()));
      this.completionRatio = this.valuePercent.map((ratio) => `${ratio.toString()}%`);
    }
    convertValueToPercent(val) {
      return Utils.Clamp(Math.abs((val - this.min.get()) / (this.max.get() - this.min.get())) * 100, 0, 100);
    }
    onMouseDown() {
      var _a13, _b;
      if (this.disabled.get()) {
        return;
      }
      (_b = (_a13 = this.props).onFocusIn) == null ? void 0 : _b.call(_a13);
      this.isButtonSelected.set(true);
      this.allowMovement = true;
      this.sliderBarRect = this.gamepadUiComponentRef.instance.getBoundingClientRect();
      document.addEventListener("mouseup", this.onGlobalMouseUp);
      document.addEventListener("mouseleave", this.onGlobalMouseUp);
      document.addEventListener("mousemove", this.onGlobalMouseMove);
    }
    onMouseWheel(e) {
      e.preventDefault();
      const precision = this.precision.get() || 1;
      const scrollDirection = this.verticalSlider ? -1 : 1;
      const step = precision * (e.deltaY < 1 ? -1 : 1) * scrollDirection;
      this.valuePercent.set(Utils.Clamp(this.valuePercent.get() + step, 0, 100));
    }
    _onGlobalMouseUp() {
      var _a13, _b;
      this.allowMovement = false;
      (_b = (_a13 = this.props).onFocusOut) == null ? void 0 : _b.call(_a13);
      this.isButtonSelected.set(false);
      document.removeEventListener("mouseup", this.onGlobalMouseUp);
      document.removeEventListener("mouseleave", this.onGlobalMouseUp);
      document.removeEventListener("mousemove", this.onGlobalMouseMove);
    }
    _onGlobalMouseMove(e) {
      const clientPos = this.verticalSlider ? e.clientY : e.clientX;
      if (!this.allowMovement || clientPos === this.mousePos) {
        return;
      }
      this.mousePos = clientPos;
      let sliderStart = this.sliderBarRect.left;
      let sliderEnd = this.sliderBarRect.right;
      let sliderSize = this.sliderBarRect.width;
      let startRatio = 0;
      if (this.verticalSlider) {
        sliderStart = this.sliderBarRect.top;
        sliderEnd = this.sliderBarRect.bottom;
        sliderSize = this.sliderBarRect.height;
        startRatio = 100;
      }
      if (clientPos < sliderStart) {
        this.valuePercent.set(startRatio);
        return;
      }
      if (clientPos > sliderEnd) {
        this.valuePercent.set(100 - startRatio);
        return;
      }
      const mousePosOnSlider = Utils.Clamp(
        Math.abs(startRatio - (clientPos - sliderStart) / sliderSize * 100),
        0,
        100
      );
      let sliderPos = mousePosOnSlider;
      const precision = this.precision.get();
      if (precision !== 0) {
        const quotient = Math.trunc((mousePosOnSlider + precision / 2) / precision);
        sliderPos = Utils.Clamp(quotient * precision, 0, 100);
      }
      this.valuePercent.set(sliderPos);
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent(
        "div",
        {
          class: {
            slider: true,
            reverse: this.min > this.max,
            vertical: this.verticalSlider,
            hoverable: this.isButtonHoverable,
            selected: this.isButtonSelected
          },
          style: { "--ratio-value": this.completionRatio },
          ref: this.gamepadUiComponentRef
        },
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "disabled-layer" }),
        /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "slider-bar", ref: this.sliderBarRef }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "slider-rail" }), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "slider-track" }), /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "slider-button" }))
      );
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      Coherent.on("mouseReleaseOutsideView", this.onGlobalMouseUp);
      this.sliderBarRef.instance.onmousedown = this.onMouseDown.bind(this);
      if (this.allowWheel) {
        this.sliderBarRef.instance.onwheel = this.onMouseWheel.bind(this);
      }
      this.subs.push(
        this.valuePercent.sub(
          (val) => this.value.set(val * (this.max.get() - this.min.get()) / 100 + this.min.get())
        ),
        this.value.sub((value2) => {
          var _a13, _b;
          (_b = (_a13 = this.props).onValueChange) == null ? void 0 : _b.call(_a13, value2);
          this.valuePercent.set(this.convertValueToPercent(value2));
        })
      );
      const sliderElement = this.gamepadUiComponentRef.getOrDefault();
      if (sliderElement !== null) {
        const inputActions = this.verticalSlider ? {
          decrease: InputAction.SCROLL_DOWN,
          increase: InputAction.SCROLL_UP
        } : {
          decrease: InputAction.SCROLL_LEFT,
          increase: InputAction.SCROLL_RIGHT
        };
        let isScrolling = false;
        const addMoveSliderInputAction = /* @__PURE__ */ __name((inputAction, factor) => this.inputManager.addInputActionOnHover(
          sliderElement,
          inputAction,
          (axis) => {
            var _a13, _b;
            if (!isScrolling) {
              (_b = (_a13 = this.props).onFocusIn) == null ? void 0 : _b.call(_a13);
              this.isButtonSelected.set(true);
              isScrolling = true;
            }
            this.valuePercent.set(this.valuePercent.get() + factor * axis * axis * 2);
            return true;
          },
          { inputType: "axis" }
        ), "addMoveSliderInputAction");
        this.decreaseSliderActionDestructor = addMoveSliderInputAction(inputActions.decrease, -1);
        this.increaseSliderActionDestructor = addMoveSliderInputAction(inputActions.increase, 1);
        const releasedInputTypeOptions = { inputType: "released" };
        const onMoveSliderInputActionReleased = /* @__PURE__ */ __name(() => {
          var _a13, _b;
          if (isScrolling) {
            (_b = (_a13 = this.props).onFocusOut) == null ? void 0 : _b.call(_a13);
            this.isButtonSelected.set(false);
            isScrolling = false;
          }
          return false;
        }, "onMoveSliderInputActionReleased");
        this.decreaseSliderActionReleasedDestructor = this.inputManager.addInputAction(
          inputActions.decrease,
          onMoveSliderInputActionReleased,
          releasedInputTypeOptions
        );
        this.increaseSliderActionReleasedDestructor = this.inputManager.addInputAction(
          inputActions.increase,
          onMoveSliderInputActionReleased,
          releasedInputTypeOptions
        );
      }
    }
    destroy() {
      var _a13, _b, _c, _d;
      (_a13 = this.decreaseSliderActionDestructor) == null ? void 0 : _a13.call(this);
      (_b = this.increaseSliderActionDestructor) == null ? void 0 : _b.call(this);
      (_c = this.decreaseSliderActionReleasedDestructor) == null ? void 0 : _c.call(this);
      (_d = this.increaseSliderActionReleasedDestructor) == null ? void 0 : _d.call(this);
      this.isButtonHoverable.destroy();
      this.subs.forEach((sub) => sub.destroy());
      super.destroy();
    }
  };
  __name(_Slider, "Slider");
  var Slider = _Slider;
  var _Switch = class _Switch extends GamepadUiComponent {
    constructor() {
      var _a13;
      super(...arguments);
      this.turnOnDirection = (_a13 = this.props.turnOnDirection) != null ? _a13 : "right";
      this.checked = import_msfs_sdk.SubscribableUtils.isMutableSubscribable(this.props.checked) ? this.props.checked : import_msfs_sdk.Subject.create(!!this.props.checked);
      this.turnOnDirectionSub = this.turnOnDirection === "right" ? this.checked : this.checked.map(import_msfs_sdk.SubscribableMapFunctions.not());
      this.sliderRef = import_msfs_sdk.FSComponent.createRef();
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.gamepadUiComponentRef.instance.onclick = () => {
        if (import_msfs_sdk.SubscribableUtils.toSubscribable(this.props.disabled, true).get() === true) {
          return;
        }
        this.checked.set(!this.checked.get());
      };
      this.checked.sub((checked) => {
        if (this.props.callback) this.props.callback(checked);
      });
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: { "switch-container": true, checked: this.checked }, ref: this.gamepadUiComponentRef }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: { slider: true, "slider--right": this.turnOnDirectionSub }, ref: this.sliderRef }, /* @__PURE__ */ import_msfs_sdk.FSComponent.buildComponent("div", { class: "disabled-layer" })));
    }
  };
  __name(_Switch, "Switch");
  var Switch = _Switch;
  var _a10;
  var _OnboardingManager = (_a10 = class {
    constructor() {
      this.isStarted = false;
      this.stepIndex = 0;
      this.steps = [];
    }
    static getManager() {
      var _a13;
      return (_a13 = _a10.INSTANCE) != null ? _a13 : _a10.INSTANCE = new _a10();
    }
    bindContainer(containerRef) {
      _a10.getManager().containerRef = containerRef;
    }
    start(onboarding) {
      const onboardingManager = _a10.getManager();
      onboardingManager.steps = onboarding.steps.map((step) => {
        return __spreadProps2(__spreadValues2({}, step), { actions: [...step.actions] });
      });
      if (!onboardingManager.containerRef) {
        console.warn("Onboarding container not bound");
        return;
      }
      if (!onboardingManager.steps.length) {
        console.warn("No onboarding steps provided");
        return;
      }
      if (onboardingManager.isStarted) {
        console.warn("Onboarding already started");
        return;
      }
      onboardingManager.stepIndex = 0;
      onboardingManager.isStarted = true;
      onboardingManager.onFinish = onboarding.onFinish;
      onboardingManager.steps[onboardingManager.stepIndex].actions.unshift(
        {
          key: "@fs-base-efb,TT:EFB.COMMON.QUICK_TOUR_AROUND",
          callback: /* @__PURE__ */ __name(() => {
            onboardingManager.next();
          }, "callback")
        },
        {
          key: "@fs-base-efb,TT:EFB.COMMON.SKIP",
          callback: /* @__PURE__ */ __name(() => {
            onboardingManager.stop();
          }, "callback")
        }
      );
      onboardingManager.containerRef.instance.show();
      onboardingManager.containerRef.instance.setStep(onboardingManager.steps[onboardingManager.stepIndex]);
      onboardingManager.stepIndex++;
      return;
    }
    next() {
      var _a13;
      const onboardingManager = _a10.getManager();
      if (!onboardingManager.isStarted) {
        console.warn("Onboarding not started. You should call start method first");
        return;
      }
      if (onboardingManager.stepIndex >= onboardingManager.steps.length) {
        onboardingManager.stop();
        return;
      }
      onboardingManager.steps[onboardingManager.stepIndex].actions.unshift({
        key: "@fs-base-efb,TT:EFB.COMMON.NEXT",
        callback: /* @__PURE__ */ __name(() => {
          onboardingManager.next();
        }, "callback")
      });
      (_a13 = onboardingManager.containerRef) == null ? void 0 : _a13.instance.setStep(onboardingManager.steps[onboardingManager.stepIndex]);
      onboardingManager.stepIndex++;
      return;
    }
    stop() {
      var _a13, _b;
      const onboardingManager = _a10.getManager();
      onboardingManager.isStarted = false;
      onboardingManager.steps = [];
      (_a13 = onboardingManager.containerRef) == null ? void 0 : _a13.instance.hide();
      (_b = onboardingManager.onFinish) == null ? void 0 : _b.call(onboardingManager);
    }
  }, __name(_a10, "_OnboardingManager"), _a10);
  _OnboardingManager.INSTANCE = void 0;
  var _a11;
  var _EfbCommonSettingsManager = (_a11 = class extends import_msfs_sdk.DefaultUserSettingManager {
    constructor() {
      super(...arguments);
      this.debounceTimer = new import_msfs_sdk.DebounceTimer();
      this.ignoredForceSaveSettingKeys = [];
    }
    /** @inheritdoc */
    onSettingValueChanged(entry, value2) {
      super.onSettingValueChanged(entry, value2);
      if (!this.ignoredForceSaveSettingKeys.includes(entry.setting.definition.name)) {
        this.debounceTimer.schedule(() => {
          Coherent.trigger("EFB_PERSISTENT_SETTINGS_SAVE");
        }, _a11.debounceTimeout);
      }
    }
  }, __name(_a11, "_EfbCommonSettingsManager"), _a11);
  _EfbCommonSettingsManager.debounceTimeout = 5e3;
  var EfbCommonSettingsManager = _EfbCommonSettingsManager;
  var UnitsNavAngleSettingMode = /* @__PURE__ */ ((UnitsNavAngleSettingMode2) => {
    UnitsNavAngleSettingMode2["Magnetic"] = "magnetic";
    UnitsNavAngleSettingMode2["True"] = "true";
    return UnitsNavAngleSettingMode2;
  })(UnitsNavAngleSettingMode || {});
  var UnitsSpeedSettingMode = /* @__PURE__ */ ((UnitsSpeedSettingMode2) => {
    UnitsSpeedSettingMode2["Nautical"] = "KTS";
    UnitsSpeedSettingMode2["Metric"] = "KPH";
    return UnitsSpeedSettingMode2;
  })(UnitsSpeedSettingMode || {});
  var UnitsDistanceSettingMode = /* @__PURE__ */ ((UnitsDistanceSettingMode2) => {
    UnitsDistanceSettingMode2["Nautical"] = "NM";
    UnitsDistanceSettingMode2["Metric"] = "KM";
    return UnitsDistanceSettingMode2;
  })(UnitsDistanceSettingMode || {});
  var UnitsSmallDistanceSettingMode = /* @__PURE__ */ ((UnitsSmallDistanceSettingMode2) => {
    UnitsSmallDistanceSettingMode2["Feet"] = "FT";
    UnitsSmallDistanceSettingMode2["Meters"] = "M";
    return UnitsSmallDistanceSettingMode2;
  })(UnitsSmallDistanceSettingMode || {});
  var UnitsAltitudeSettingMode = /* @__PURE__ */ ((UnitsAltitudeSettingMode2) => {
    UnitsAltitudeSettingMode2["Feet"] = "FT";
    UnitsAltitudeSettingMode2["Meters"] = "M";
    return UnitsAltitudeSettingMode2;
  })(UnitsAltitudeSettingMode || {});
  var UnitsWeightSettingMode = /* @__PURE__ */ ((UnitsWeightSettingMode2) => {
    UnitsWeightSettingMode2["Pounds"] = "LBS";
    UnitsWeightSettingMode2["Kilograms"] = "KG";
    return UnitsWeightSettingMode2;
  })(UnitsWeightSettingMode || {});
  var UnitsVolumeSettingMode = /* @__PURE__ */ ((UnitsVolumeSettingMode2) => {
    UnitsVolumeSettingMode2["Gallons"] = "GAL US";
    UnitsVolumeSettingMode2["Liters"] = "L";
    return UnitsVolumeSettingMode2;
  })(UnitsVolumeSettingMode || {});
  var UnitsTemperatureSettingMode = /* @__PURE__ */ ((UnitsTemperatureSettingMode2) => {
    UnitsTemperatureSettingMode2["Fahrenheit"] = "\xB0F";
    UnitsTemperatureSettingMode2["Celsius"] = "\xB0C";
    return UnitsTemperatureSettingMode2;
  })(UnitsTemperatureSettingMode || {});
  var UnitsTimeSettingMode = /* @__PURE__ */ ((UnitsTimeSettingMode2) => {
    UnitsTimeSettingMode2["Local12"] = "local-12";
    UnitsTimeSettingMode2["Local24"] = "local-24";
    return UnitsTimeSettingMode2;
  })(UnitsTimeSettingMode || {});
  var _a12;
  var _UnitsSettingsManager = (_a12 = class extends EfbCommonSettingsManager {
    constructor(bus, settingsDefs) {
      super(bus, settingsDefs, true);
      this.navAngleUnitsSub = import_msfs_sdk.Subject.create(_a12.MAGNETIC_BEARING);
      this.navAngleUnits = this.navAngleUnitsSub;
      this.timeUnitsSub = import_msfs_sdk.Subject.create(
        "local-12"
        /* Local12 */
      );
      this._timeUnits = this.timeUnitsSub;
      this.areSubscribablesInit = false;
      this.areSubscribablesInit = true;
      for (const entry of this.settings.values()) {
        this.updateUnitsSubjects(entry.setting.definition.name, entry.setting.value);
      }
    }
    onSettingValueChanged(entry, value2) {
      if (this.areSubscribablesInit) {
        this.updateUnitsSubjects(entry.setting.definition.name, value2);
      }
      super.onSettingValueChanged(entry, value2);
    }
    /**
     * Checks if the values loaded from the datastorage correspond to the settings types.
     */
    checkLoadedValues() {
      checkUserSetting(this.getSetting("unitsNavAngle"), UnitsNavAngleSettingMode);
      checkUserSetting(this.getSetting("unitsSpeed"), UnitsSpeedSettingMode);
      checkUserSetting(this.getSetting("unitsDistance"), UnitsDistanceSettingMode);
      checkUserSetting(this.getSetting("unitsAltitude"), UnitsAltitudeSettingMode);
      checkUserSetting(this.getSetting("unitsSmallDistance"), UnitsSmallDistanceSettingMode);
      checkUserSetting(this.getSetting("unitsWeight"), UnitsWeightSettingMode);
      checkUserSetting(this.getSetting("unitsVolume"), UnitsVolumeSettingMode);
      checkUserSetting(this.getSetting("unitsTemperature"), UnitsTemperatureSettingMode);
      checkUserSetting(this.getSetting("unitsTime"), UnitsTimeSettingMode);
    }
    updateUnitsSubjects(settingName, value2) {
      switch (settingName) {
        case "unitsNavAngle":
          this.navAngleUnitsSub.set(
            value2 === "true" ? _a12.TRUE_BEARING : _a12.MAGNETIC_BEARING
          );
          break;
        case "unitsTime":
          this.timeUnitsSub.set(value2);
          break;
        case "unitsDistance":
          this.getSetting("unitsSmallDistance").set(
            value2 === "NM" ? "FT" : "M"
            /* Meters */
          );
      }
    }
    /**
     * Gets a unit type subscribable from a unit setting name
     * @param settingName the name of the unit setting
     * @returns a Subscribable containing the unit type. If the value in the dataStorage is unvalid, it returns the default unitType
     */
    getSettingUnitType(settingName) {
      const setting = this.getSetting(settingName);
      return setting.map(
        (settingValue) => {
          var _a13;
          return (_a13 = UnitTypesMap[settingValue]) != null ? _a13 : UnitTypesMap[setting.definition.defaultValue];
        }
      );
    }
  }, __name(_a12, "_UnitsSettingsManager"), _a12);
  _UnitsSettingsManager.TRUE_BEARING = import_msfs_sdk.BasicNavAngleUnit.create(false);
  _UnitsSettingsManager.MAGNETIC_BEARING = import_msfs_sdk.BasicNavAngleUnit.create(true);
  var UnitTypesMap = {
    /** Mapped speed unit types */
    KTS: import_msfs_sdk.UnitType.KNOT,
    KPH: import_msfs_sdk.UnitType.KPH,
    /** Mapped distance unit type */
    NM: import_msfs_sdk.UnitType.NMILE,
    KM: import_msfs_sdk.UnitType.KILOMETER,
    /** Mapped altitude unit type */
    FT: import_msfs_sdk.UnitType.FOOT,
    M: import_msfs_sdk.UnitType.METER,
    /** Mapped weight unit type */
    LBS: import_msfs_sdk.UnitType.POUND,
    KG: import_msfs_sdk.UnitType.KILOGRAM,
    /** Mapped volume unit type */
    "GAL US": import_msfs_sdk.UnitType.GALLON,
    L: import_msfs_sdk.UnitType.LITER,
    /** Mapped temperature unit type */
    "\xB0C": import_msfs_sdk.UnitType.CELSIUS,
    "\xB0F": import_msfs_sdk.UnitType.FAHRENHEIT
  };

  // src/AtmoWeather.tsx
  var import_msfs_sdk3 = __toESM(require_msfs_sdk());

  // src/Settings.tsx
  var import_msfs_sdk2 = __toESM(require_msfs_sdk());

  // src/defaults.ts
  var DEFAULTS = {
    T0: 1,
    T1: 1.5,
    T2: 0.15,
    T3: 4,
    T4: 0.6,
    T5: 2.5,
    T6: 0.05,
    T7: 0.1,
    T8: 1,
    T9: 0.7,
    T10: 1.3,
    T11: 1,
    T12: 1,
    T13: 1,
    T14: 0,
    T15: 1,
    C0: 1,
    C1: 0.9,
    C2: 0.5,
    C3: 0.4,
    C4: 0.4,
    C5: 0.12,
    C6: 0.5,
    C7: 150,
    C8: 0,
    C9: 0.5,
    C10: 4,
    C11: 0.08,
    C12: 0.4,
    C16: 2,
    C17: 0.33,
    C18: 0,
    C19: 1.5,
    C20: 1,
    C21: 50,
    C22: 1,
    C23: 1,
    C24: 0.6,
    C25: 0.5,
    C26: 0.4,
    C27: 0.3,
    C28: 1,
    C29: 300,
    C30: 0,
    C31: 600,
    C32: 0
  };

  // src/Settings.tsx
  var EVENT_SETTINGS = "AtmosphereMod.Settings";
  var EVENT_STATE = "AtmosphereMod.State";
  var clamp = /* @__PURE__ */ __name((x, lo, hi) => Math.min(Math.max(x, lo), hi), "clamp");
  var identity = /* @__PURE__ */ __name((x) => x, "identity");
  function percent(key, label, tip, scale, more) {
    return {
      kind: "slider",
      key,
      label,
      tip,
      min: 0,
      max: 100,
      step: 5,
      more,
      toDisplay: /* @__PURE__ */ __name((raw) => raw / scale * 100, "toDisplay"),
      toRaw: /* @__PURE__ */ __name((d) => d / 100 * scale, "toRaw"),
      format: /* @__PURE__ */ __name((d) => `${Math.round(d)} %`, "format")
    };
  }
  __name(percent, "percent");
  function plain(key, label, tip, min, max, step, digits, extra = {}) {
    return __spreadValues({
      kind: "slider",
      key,
      label,
      tip,
      min,
      max,
      step,
      toDisplay: identity,
      toRaw: identity,
      format: /* @__PURE__ */ __name((d) => d <= 0 && min <= 0 ? "off" : d.toFixed(digits), "format")
    }, extra);
  }
  __name(plain, "plain");
  var LOBE_DEG = 0.77 * (180 / Math.PI);
  var GLORY_DEG_UM = 24;
  var SIM_DEEP_LIGHT = 0.033;
  var SIM_GLARE = 0.8;
  var GLARE_LO = 0.5;
  var GLARE_HI = 0.85;
  var HAZE_COLOURS = [[0, "grey"], [0.3, "humid white"], [1.3, "continental blue"], [2, "smoke"]];
  var TABS = [
    {
      name: "Haze",
      more: "Manual haze (when not from the METAR)",
      settings: [
        {
          kind: "switch",
          key: "T0",
          label: "Atmosphere (haze and sky)",
          tip: "The mod's haze and sky. Off: the sim's own atmosphere, exactly."
        },
        {
          kind: "switch",
          key: "T13",
          label: "Haze from METAR",
          tip: "The haze follows the sim's own weather reports (METARs): its amount, height and colour, blended between stations as you fly. Off: the manual haze settings below."
        },
        {
          kind: "switch",
          key: "T11",
          label: "Distant clouds fade creamy, not grey",
          tip: "Distant clouds fade colour by colour, as the haze scatters blue light out of the view, so they turn creamy like real ones. Off: the sim's grey dimming."
        },
        {
          kind: "slider",
          key: "T9",
          label: "Sun glare in haze",
          min: 0,
          max: 100,
          step: 5,
          tip: "How much the haze glares around the sun: higher gives a brighter, tighter glow looking toward it (the sim's own is about 85 %). With haze from METAR, the weather adds its share to this.",
          toDisplay: /* @__PURE__ */ __name((raw) => (clamp(raw > 0 ? raw : SIM_GLARE, GLARE_LO, GLARE_HI) - GLARE_LO) / (GLARE_HI - GLARE_LO) * 100, "toDisplay"),
          toRaw: /* @__PURE__ */ __name((d) => GLARE_LO + d / 100 * (GLARE_HI - GLARE_LO), "toRaw"),
          format: /* @__PURE__ */ __name((d) => `${Math.round(d)} %`, "format")
        },
        plain(
          "T3",
          "Haze near the ground",
          "How hazy the air near the ground is, as a multiple of the sim's own surface haze: about \xD74 on a clean mountain day, \xD78-14 in hazy or polluted lowlands.",
          1,
          15,
          0.5,
          1,
          { format: /* @__PURE__ */ __name((d) => `\xD7${d.toFixed(1)}`, "format"), manualHaze: true, more: true }
        ),
        plain(
          "T1",
          "Haze layer top",
          "The height above sea level where the hazy layer near the ground ends. Above it the air is clearer and the sky bluer.",
          0.5,
          4,
          0.1,
          1,
          { format: /* @__PURE__ */ __name((d) => `${d.toFixed(1)} km`, "format"), manualHaze: true, more: true }
        ),
        plain(
          "T4",
          "Haze above the layer",
          "How hazy the air is above that layer, as a multiple of the sim's surface haze: lower gives clearer views at altitude.",
          0,
          2,
          0.1,
          1,
          { format: /* @__PURE__ */ __name((d) => `\xD7${d.toFixed(1)}`, "format"), manualHaze: true, more: true }
        ),
        plain(
          "T6",
          "Brown haze (pollution)",
          "Light-absorbing particles such as soot and smog in the layer near the ground: a darker, brownish haze. 0 is none; 0.15-0.3 suits polluted lowlands.",
          0,
          0.4,
          0.01,
          2,
          { manualHaze: true, more: true }
        ),
        plain(
          "T10",
          "Haze colour",
          "The haze's colour, set by its particle size: grey (0), humid or sea-air white (0.3), continental blue (1.3), smoke (2).",
          0,
          2,
          0.1,
          1,
          {
            manualHaze: true,
            more: true,
            format: /* @__PURE__ */ __name((d) => {
              const nearest = HAZE_COLOURS.reduce((a, b) => Math.abs(b[0] - d) < Math.abs(a[0] - d) ? b : a);
              return `${d.toFixed(1)} ${nearest[1]}`;
            }, "format")
          }
        )
      ]
    },
    {
      name: "Sky & light",
      settings: [
        {
          kind: "switch",
          key: "T8",
          label: "Real-world sky colours",
          tip: "Sky colours fitted to the real sky across the whole spectrum: a blue sky overhead at sunset and the right orange of a low sun, with ozone by season and latitude. Off: the sim's own colours."
        },
        {
          kind: "switch",
          key: "C22",
          label: "Daylight white balance",
          tip: "The sim's sunlight has its colour from above the atmosphere, slightly warm on screen, so sunlit clouds look cream. On: sunlight looks white, as your eyes see it; sky, clouds and ground shift alike."
        }
      ]
    },
    {
      name: "Cloud lighting",
      settings: [
        {
          kind: "switch",
          key: "C0",
          label: "Cloud lighting",
          tip: "How the mod draws clouds: this tab's lighting, solid clouds and cloud types. Off: the sim's own clouds, exactly. (The cloud shapes from METARs and the thunderstorms stay; see the Clouds tab.)"
        },
        percent(
          "C2",
          "Silver lining strength",
          "How bright the silver lining is: the glowing rim of clouds close to the sun. 0 % is the sim's own.",
          0.5
        ),
        {
          kind: "slider",
          key: "C1",
          label: "Silver lining size",
          min: 1,
          max: 7,
          step: 0.1,
          tip: "How far from the sun the silver lining reaches (its half width). Real cloud droplets give about 1-2\xB0.",
          toDisplay: /* @__PURE__ */ __name((raw) => clamp((1 - raw) * LOBE_DEG, 1, 7), "toDisplay"),
          toRaw: /* @__PURE__ */ __name((d) => 1 - d / LOBE_DEG, "toRaw"),
          format: /* @__PURE__ */ __name((d) => `${d.toFixed(1)}\xB0`, "format")
        },
        plain(
          "C9",
          "Glory",
          "Coloured rings around the point opposite the sun, where your shadow falls on cloud tops: blue inside, red outside. Off at 0.",
          0,
          2,
          0.1,
          1
        ),
        percent(
          "C12",
          "Your aircraft's shadow on clouds",
          "Your aircraft's shadow on the clouds below, at the centre of the glory: how dark its core is. It blurs with distance, as a real shadow does.",
          1
        ),
        percent(
          "C3",
          "Bounced light at cloud edges",
          "Light that has bounced around inside the cloud, already at its sunlit edges: brighter cloud sides with the sun to one side, and a slightly smaller silver lining.",
          1,
          true
        ),
        {
          kind: "slider",
          key: "C5",
          label: "Light deep inside clouds",
          min: 1,
          max: 6,
          step: 0.1,
          more: true,
          tip: "How much sunlight reaches deep inside clouds, as a multiple of the sim's own: brighter interiors and bases.",
          toDisplay: /* @__PURE__ */ __name((raw) => clamp((raw > 0 ? raw : SIM_DEEP_LIGHT) / SIM_DEEP_LIGHT, 1, 6), "toDisplay"),
          toRaw: /* @__PURE__ */ __name((d) => d * SIM_DEEP_LIGHT, "toRaw"),
          format: /* @__PURE__ */ __name((d) => `\xD7${d.toFixed(1)}`, "format")
        },
        percent(
          "C6",
          "Powder effect (grey rims, sun behind you)",
          "With the sun behind you, cloud edges and thin parts look grey and dense cores white, which outlines each turret. It fades out toward the sun.",
          1,
          true
        ),
        plain(
          "C8",
          "Crisper cloud edges",
          "Sharper cloud outlines: the sim fades every cloud edge over 220 m or more; 1 halves that, 3 quarters it. Far clouds keep softer edges, against flicker.",
          0,
          4,
          0.25,
          2,
          { more: true }
        ),
        {
          kind: "slider",
          key: "C10",
          label: "Glory ring size",
          min: 1.5,
          max: 8,
          step: 0.5,
          more: true,
          tip: "The glory's size, as the angle of its first red ring: smaller cloud droplets make larger rings.",
          toDisplay: /* @__PURE__ */ __name((raw) => clamp(GLORY_DEG_UM / (raw > 0 ? raw : 10), 1.5, 8), "toDisplay"),
          toRaw: /* @__PURE__ */ __name((d) => GLORY_DEG_UM / d, "toRaw"),
          format: /* @__PURE__ */ __name((d) => `${d.toFixed(1)}\xB0`, "format")
        }
      ]
    },
    {
      name: "Clouds",
      settings: [
        percent(
          "C20",
          "Clouds follow the METARs",
          "Near weather stations, the sim's live clouds take the layers the stations report: base, cover, CB and TCU. 100 % is the reported layers within the station radius. Live weather only.",
          1
        ),
        plain(
          "C21",
          "Station radius",
          "Each station's clouds apply fully within this distance and fade out over the same distance beyond.",
          10,
          100,
          5,
          0,
          { format: /* @__PURE__ */ __name((d) => `${Math.round(d)} km`, "format") }
        ),
        plain(
          "C16",
          "Solid clouds",
          "Clouds as thick as real ones: the sim's are see-through at bases and edges, and the sun glows through an overcast. Thickness at a cumulus core (1 is the sim's); only the look, not the lighting.",
          0,
          4,
          0.25,
          2,
          { format: /* @__PURE__ */ __name((d) => d <= 0 ? "the sim's" : d.toFixed(2), "format") }
        ),
        percent(
          "C23",
          "Cloud types",
          "Tells cloud types apart by height, depth and cover: dense, crisp cumulus; thinner, softer stratocumulus, stratus, altocumulus and altostratus. 0 % draws every cloud like cumulus. Live weather only.",
          1
        ),
        percent(
          "C28",
          "Smooth cloud bases",
          "Flat cloud bases: the sim's round puffs fade out toward each cloud's base, while the tops keep their billows.",
          1
        ),
        {
          kind: "switch",
          key: "C32",
          label: "Thunderstorms (experimental)",
          tip: "Storm clouds where a station nearby reports thunder or CB/TCU, or the sim's rain is very heavy: towers up to the tropopause with anvils, each living 45-80 min. Visual only; live weather only."
        },
        {
          kind: "slider",
          key: "C17",
          label: "Cloud bases: soft to solid",
          min: 0,
          max: 1,
          step: 0.05,
          more: true,
          tip: `How quickly clouds get dense above their base. The sim's build up over 3 km, so bases and thin decks are nearly transparent; "real" is solid from the base up.`,
          toDisplay: /* @__PURE__ */ __name((raw) => clamp((1 - (raw > 0 ? raw : 1)) / 0.67, 0, 1), "toDisplay"),
          toRaw: /* @__PURE__ */ __name((d) => 1 - 0.67 * d, "toRaw"),
          format: /* @__PURE__ */ __name((d) => d <= 0.01 ? "the sim's" : d >= 0.99 ? "real" : `${Math.round(d * 100)} %`, "format")
        },
        plain(
          "C19",
          "High-cloud thickness",
          "How thick high clouds look (cirrus and thin sheets, which keep the sim's translucent look): higher shows more cirrus, but icy layers at cruise get more fog-like.",
          0.5,
          3,
          0.1,
          1,
          { format: /* @__PURE__ */ __name((d) => `\xD7${d.toFixed(1)}`, "format"), needs: "C16", more: true }
        ),
        __spreadProps(__spreadValues({}, percent(
          "C24",
          "Stratocumulus thickness",
          "How solid stratocumulus (lumpy low decks) is, as a share of Solid clouds.",
          1,
          true
        )), {
          needs: "C23"
        }),
        __spreadProps(__spreadValues({}, percent(
          "C25",
          "Stratus thickness",
          "How solid stratus (flat, grey low decks) is, as a share of Solid clouds.",
          1,
          true
        )), {
          needs: "C23"
        }),
        __spreadProps(__spreadValues({}, percent(
          "C26",
          "Altocumulus thickness",
          "How solid altocumulus (mid-level patches and rolls) is, as a share of Solid clouds.",
          1,
          true
        )), {
          needs: "C23"
        }),
        __spreadProps(__spreadValues({}, percent(
          "C27",
          "Altostratus thickness",
          "How solid altostratus (a mid-level veil the sun can shine through) is, as a share of Solid clouds.",
          1,
          true
        )), {
          needs: "C23"
        })
      ]
    }
  ];
  var EDIT_HOLD_MS = 1500;
  var SEND_MS = 100;
  var _SettingsModel = class _SettingsModel {
    constructor(send) {
      this.send = send;
      /** The add-on has answered. */
      this.connected = import_msfs_sdk2.Subject.create(false);
      /** The Ctrl+Shift+F9 switch. */
      this.enabled = import_msfs_sdk2.Subject.create(true);
      this.weather = import_msfs_sdk2.Subject.create("");
      this.raw = /* @__PURE__ */ new Map();
      this.effective = /* @__PURE__ */ new Map();
      this.editedAt = /* @__PURE__ */ new Map();
      this.pending = {};
    }
    /** tunables.ini's value. */
    value(key) {
      var _a13;
      let s = this.raw.get(key);
      if (s === void 0) {
        s = import_msfs_sdk2.Subject.create((_a13 = DEFAULTS[key]) != null ? _a13 : 0);
        this.raw.set(key, s);
      }
      return s;
    }
    /** The value the shaders get (the METAR's haze blended in); T only. */
    effectiveValue(key) {
      let s = this.effective.get(key);
      if (s === void 0) {
        s = import_msfs_sdk2.Subject.create(this.value(key).get());
        this.effective.set(key, s);
      }
      return s;
    }
    requestState() {
      this.send({ get: true });
    }
    onState(args) {
      var _a13;
      let state;
      try {
        state = JSON.parse(args);
      } catch (e) {
        return;
      }
      const now = Date.now();
      const apply = /* @__PURE__ */ __name((prefix, values, target, edits) => {
        values == null ? void 0 : values.forEach((v, i) => {
          var _a14;
          const key = `${prefix}${i}`;
          if (!edits || now - ((_a14 = this.editedAt.get(key)) != null ? _a14 : 0) > EDIT_HOLD_MS) {
            target(key).set(v);
          }
        });
      }, "apply");
      apply("T", state.T, (k) => this.value(k), true);
      apply("C", state.C, (k) => this.value(k), true);
      apply("T", state.TE, (k) => this.effectiveValue(k), false);
      if (state.enabled !== void 0) {
        this.enabled.set(state.enabled);
      }
      this.weather.set((_a13 = state.weather) != null ? _a13 : "");
      this.connected.set(true);
    }
    /** Sets tunables.ini's value: at once here, in the add-on within SEND_MS. */
    set(key, raw) {
      const rounded = Math.round(raw * 1e4) / 1e4;
      this.value(key).set(rounded);
      this.editedAt.set(key, Date.now());
      this.pending[key] = rounded;
      if (this.timer === void 0) {
        this.timer = window.setTimeout(() => {
          this.timer = void 0;
          const set = this.pending;
          this.pending = {};
          this.send({ set });
        }, SEND_MS);
      }
    }
    setEnabled(on) {
      this.enabled.set(on);
      this.send({ enabled: on });
    }
    /** As Ctrl+Shift+F10: the add-on saves pending changes, then reloads tunables.ini and its shaders. */
    reload() {
      this.send({ reload: true });
    }
    reset(keys) {
      for (const key of keys) {
        if (DEFAULTS[key] !== void 0) {
          this.set(key, DEFAULTS[key]);
        }
      }
    }
  };
  __name(_SettingsModel, "SettingsModel");
  var SettingsModel = _SettingsModel;
  var _Quiet = class _Quiet {
    constructor() {
      this.depth = 0;
    }
    get active() {
      return this.depth > 0;
    }
    run(f) {
      this.depth++;
      try {
        f();
      } finally {
        this.depth--;
      }
    }
  };
  __name(_Quiet, "Quiet");
  var Quiet = _Quiet;
  var _Label = class _Label extends import_msfs_sdk2.DisplayComponent {
    render() {
      return /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-label" }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("span", { class: "atmo-name" }, this.props.text), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-tip" }, this.props.tip));
    }
  };
  __name(_Label, "Label");
  var Label = _Label;
  var _SliderRow = class _SliderRow extends import_msfs_sdk2.DisplayComponent {
    constructor(props) {
      var _a13;
      super(props);
      this.display = import_msfs_sdk2.Subject.create(0);
      this.text = import_msfs_sdk2.Subject.create("");
      this.quiet = new Quiet();
      this.subs = [];
      const { model, spec } = props;
      this.disabled = import_msfs_sdk2.MappedSubject.create(
        ([live, metar, needed]) => !live || spec.manualHaze === true && metar >= 0.5 || spec.needs !== void 0 && needed <= 0,
        props.live,
        model.value("T13"),
        model.value((_a13 = spec.needs) != null ? _a13 : "T0")
      );
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      const { model, spec } = this.props;
      const shown = import_msfs_sdk2.MappedSubject.create(
        ([raw, effective, metar]) => spec.toDisplay(spec.manualHaze === true && metar >= 0.5 ? effective : raw),
        model.value(spec.key),
        spec.manualHaze === true ? model.effectiveValue(spec.key) : model.value(spec.key),
        model.value("T13")
      );
      this.subs.push(shown.sub((d) => this.quiet.run(() => this.display.set(d)), true));
      this.subs.push(this.display.sub((d) => this.text.set(spec.format(d)), true));
    }
    onChange(d) {
      if (!this.quiet.active && !this.disabled.get()) {
        this.props.model.set(this.props.spec.key, this.props.spec.toRaw(d));
      }
    }
    render() {
      const { spec } = this.props;
      return /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: { "atmo-row": true, "atmo-disabled": this.disabled } }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(Label, { text: spec.label, tip: spec.tip }), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-control" }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(
        Slider,
        {
          value: this.display,
          min: spec.min,
          max: spec.max,
          step: spec.step,
          disabled: this.disabled,
          onValueChange: (d) => this.onChange(d)
        }
      )), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-value" }, this.text));
    }
    destroy() {
      this.subs.forEach((s) => s.destroy());
      super.destroy();
    }
  };
  __name(_SliderRow, "SliderRow");
  var SliderRow = _SliderRow;
  var _SwitchRow = class _SwitchRow extends import_msfs_sdk2.DisplayComponent {
    constructor() {
      super(...arguments);
      this.checked = import_msfs_sdk2.Subject.create(false);
      this.quiet = new Quiet();
      this.disabled = this.props.live.map((live) => !live);
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.sub = this.props.model.value(this.props.spec.key).sub((v) => this.quiet.run(() => this.checked.set(v >= 0.5)), true);
    }
    onChange(on) {
      if (!this.quiet.active) {
        this.props.model.set(this.props.spec.key, on ? 1 : 0);
      }
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: { "atmo-row": true, "atmo-disabled": this.disabled } }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(Label, { text: this.props.spec.label, tip: this.props.spec.tip }), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-control atmo-switch" }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(Switch, { checked: this.checked, disabled: this.disabled, callback: (on) => this.onChange(on) })));
    }
    destroy() {
      var _a13;
      (_a13 = this.sub) == null ? void 0 : _a13.destroy();
      super.destroy();
    }
  };
  __name(_SwitchRow, "SwitchRow");
  var SwitchRow = _SwitchRow;
  function row(model, spec, live) {
    return spec.kind === "slider" ? /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(SliderRow, { model, spec, live }) : /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(SwitchRow, { model, spec, live });
  }
  __name(row, "row");
  var _SettingsView = class _SettingsView extends import_msfs_sdk2.DisplayComponent {
    constructor() {
      super(...arguments);
      this.tab = import_msfs_sdk2.Subject.create(0);
      this.masterOn = import_msfs_sdk2.Subject.create(true);
      this.quiet = new Quiet();
      this.subs = [];
    }
    onAfterRender(node) {
      super.onAfterRender(node);
      this.subs.push(this.props.model.enabled.sub((on) => this.quiet.run(() => this.masterOn.set(on)), true));
    }
    keys(tab) {
      return tab.settings.map((s) => s.key);
    }
    render() {
      const { model } = this.props;
      const live = model.connected;
      const notLive = live.map((c) => !c);
      return /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-settings" }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-row atmo-master" }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(
        Label,
        {
          text: /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("span", null, "AtmosphereMod ", /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("span", { class: "atmo-hint" }, "(Ctrl+Shift+F9)")),
          tip: "The whole mod on or off, also with Ctrl+Shift+F9. Off: the sim's own shaders, to compare."
        }
      ), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-control atmo-switch" }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(
        Switch,
        {
          checked: this.masterOn,
          disabled: notLive,
          callback: (on) => {
            if (!this.quiet.active) model.setEnabled(on);
          }
        }
      ))), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-weather" }, import_msfs_sdk2.MappedSubject.create(
        ([c, w]) => !c ? "Waiting for AtmosphereMod (is the sim running with ReShade and the mod installed?)" : w !== "" ? `Weather: ${w}` : "Weather: waiting for the first METAR",
        live,
        model.weather
      )), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-tabs" }, TABS.map((t, i) => /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(
        TabSelector,
        {
          tabName: t.name,
          active: this.tab.map((n) => n === i),
          hidden: import_msfs_sdk2.Subject.create(false),
          callback: () => this.tab.set(i)
        }
      ))), TABS.map((t, i) => {
        var _a13;
        return /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: { "atmo-page": true, "atmo-hidden": this.tab.map((n) => n !== i) } }, t.settings.filter((s) => !s.more).map((s) => row(model, s, live)), t.settings.some((s) => s.more) ? /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(ElementAccordion, { header: /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("span", { class: "atmo-more" }, (_a13 = t.more) != null ? _a13 : "More") }, t.settings.filter((s) => s.more).map((s) => row(model, s, live))) : null, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent("div", { class: "atmo-buttons" }, /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(TTButton, { key: "Reset this tab", type: "secondary", disabled: notLive, callback: () => model.reset(this.keys(t)) }), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(
          TTButton,
          {
            key: "Reset all",
            type: "secondary",
            disabled: notLive,
            callback: () => TABS.forEach((tab) => model.reset(this.keys(tab)))
          }
        ), /* @__PURE__ */ import_msfs_sdk2.FSComponent.buildComponent(TTButton, { key: "Reload tunables.ini", type: "secondary", disabled: notLive, callback: () => model.reload() })));
      }));
    }
    destroy() {
      this.subs.forEach((s) => s.destroy());
      super.destroy();
    }
  };
  __name(_SettingsView, "SettingsView");
  var SettingsView = _SettingsView;
  var SETTINGS_CSS = `
.atmo-settings { padding: 96px 32px 24px; color: #e6e6e6; font-size: 20px; height: 100%; overflow-y: auto; box-sizing: border-box; }
.atmo-row { display: flex; align-items: center; min-height: 52px; position: relative; }
.atmo-label { flex: 0 0 40%; padding-right: 16px; }
.atmo-name { border-bottom: 1px dotted rgba(255, 255, 255, 0.35); }
.atmo-tip { display: none; position: absolute; left: 40%; right: 0; top: 2px; z-index: 20; pointer-events: none;
  padding: 10px 14px; background-color: rgb(15, 20, 27); border: 1px solid rgb(63, 67, 73); border-radius: 6px;
  color: #e6e6e6; font-size: 17px; line-height: 1.35; }
.atmo-label:hover .atmo-tip { display: block; }
.atmo-control { flex: 1 1 auto; }
.atmo-control > .slider { width: 100%; }
.atmo-switch { display: flex; justify-content: flex-end; }
.atmo-value { flex: 0 0 170px; text-align: right; padding-left: 16px; font-variant-numeric: tabular-nums; }
.atmo-disabled .atmo-name, .atmo-disabled .atmo-value { opacity: 0.45; }
.atmo-master { border-bottom: 1px solid rgba(255, 255, 255, 0.15); margin-bottom: 8px; font-size: 22px; }
.atmo-hint { opacity: 0.6; font-size: 16px; }
.atmo-weather { opacity: 0.8; font-size: 17px; margin-bottom: 16px; }
.atmo-tabs { display: flex; gap: 8px; margin-bottom: 12px; }
.atmo-hidden { display: none; }
.atmo-more { font-size: 18px; }
.atmo-buttons { display: flex; gap: 12px; margin-top: 20px; }
`;

  // src/AtmoWeather.tsx
  var EVENT_METAR = "AtmosphereMod.Metar";
  var EVENT_STATUS = "AtmosphereMod.Status";
  var EVENT_REQUEST = "AtmosphereMod.Request";
  var POLL_MS = 6e4;
  var STATE_RETRY_MS = 3e3;
  function parseRequest(args) {
    var _a13;
    try {
      return (_a13 = JSON.parse(args)) != null ? _a13 : {};
    } catch (e) {
      return {};
    }
  }
  __name(parseRequest, "parseRequest");
  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = src;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error(`cannot load ${src}`));
      document.head.appendChild(script);
    });
  }
  __name(loadScript, "loadScript");
  var _WeatherBridge = class _WeatherBridge {
    constructor() {
      this.status = import_msfs_sdk3.Subject.create("starting");
      this.lastMetar = import_msfs_sdk3.Subject.create("(none yet)");
      this.started = false;
      this.listeners = [];
      this.onRegistered = [];
    }
    start(bus) {
      if (this.started) {
        return;
      }
      this.started = true;
      this.loader = new import_msfs_sdk3.FacilityLoader(import_msfs_sdk3.FacilityRepository.getRepository(bus));
      this.connect().catch((e) => this.status.set(`CommBus unavailable: ${e}`));
    }
    async connect() {
      let register = window.RegisterCommBusListener;
      if (typeof register !== "function") {
        await loadScript("/JS/Services/CommBus.js");
        register = window.RegisterCommBusListener;
      }
      if (typeof register !== "function") {
        throw new Error("RegisterCommBusListener is not defined");
      }
      this.commBus = register(() => {
        this.send(EVENT_STATUS, { state: "registered" });
        this.onRegistered.forEach((f) => f());
      });
      this.commBus.on(EVENT_REQUEST, (args) => void this.poll(parseRequest(args)));
      for (const [event, callback] of this.listeners) {
        this.commBus.on(event, callback);
      }
      setInterval(() => void this.poll({}), POLL_MS);
      setTimeout(() => void this.poll({}), 3e3);
      this.status.set("running");
    }
    async poll(request) {
      var _a13, _b, _c;
      const lat = (_a13 = request.lat) != null ? _a13 : SimVar.GetSimVarValue("PLANE LATITUDE", "degree latitude");
      const lon = (_b = request.lon) != null ? _b : SimVar.GetSimVarValue("PLANE LONGITUDE", "degree longitude");
      const message = { time: (/* @__PURE__ */ new Date()).toISOString(), lat, lon, ident: (_c = request.ident) != null ? _c : null };
      try {
        const metar = request.ident ? await this.loader.getMetar(request.ident) : await this.loader.searchMetar(lat, lon);
        message.metar = metar != null ? metar : null;
        this.lastMetar.set(metar ? metar.metarString : "no METAR returned");
        this.status.set(`running, last poll ${message.time}`);
      } catch (e) {
        message.error = String(e);
        this.lastMetar.set(`error: ${e}`);
      }
      this.send(EVENT_METAR, message);
    }
    /** Listens to a CommBus event from SimConnect clients (now, or once connected). */
    listen(event, callback, registered) {
      var _a13;
      this.listeners.push([event, callback]);
      (_a13 = this.commBus) == null ? void 0 : _a13.on(event, callback);
      if (registered !== void 0) {
        this.onRegistered.push(registered);
      }
    }
    send(event, data) {
      var _a13;
      (_a13 = this.commBus) == null ? void 0 : _a13.callSimConnect(event, JSON.stringify(data)).catch(() => void 0);
    }
  };
  __name(_WeatherBridge, "WeatherBridge");
  var WeatherBridge = _WeatherBridge;
  var bridge = new WeatherBridge();
  var settings = new SettingsModel((data) => bridge.send(EVENT_SETTINGS, data));
  bridge.listen(EVENT_STATE, (args) => settings.onState(args), () => settings.requestState());
  setInterval(() => {
    if (!settings.connected.get()) {
      settings.requestState();
    }
  }, STATE_RETRY_MS);
  var style = document.createElement("style");
  style.textContent = SETTINGS_CSS + `
.atmo-link { padding: 0 32px 104px; opacity: 0.6; font-size: 15px; }
.atmo-link pre { white-space: pre-wrap; font-family: monospace; margin: 4px 0 0; }
`;
  document.head.appendChild(style);
  var _AtmoWeatherView = class _AtmoWeatherView extends AppView {
    onOpen() {
      super.onOpen();
      settings.requestState();
    }
    onResume() {
      super.onResume();
      settings.requestState();
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk3.FSComponent.buildComponent("div", { style: "height: 100%; display: flex; flex-direction: column;" }, /* @__PURE__ */ import_msfs_sdk3.FSComponent.buildComponent("div", { style: "flex: 1 1 auto; min-height: 0;" }, /* @__PURE__ */ import_msfs_sdk3.FSComponent.buildComponent(SettingsView, { model: settings })), /* @__PURE__ */ import_msfs_sdk3.FSComponent.buildComponent("div", { class: "atmo-link" }, "METAR link: ", bridge.status, /* @__PURE__ */ import_msfs_sdk3.FSComponent.buildComponent("pre", null, bridge.lastMetar)));
    }
  };
  __name(_AtmoWeatherView, "AtmoWeatherView");
  var AtmoWeatherView = _AtmoWeatherView;
  var _AtmoWeather = class _AtmoWeather extends App {
    constructor() {
      super(...arguments);
      // HOT: created and mounted when the EFB starts, so the bridge runs without
      // the app being opened; SLEEP keeps it alive when another app is shown.
      this.BootMode = AppBootMode.HOT;
      this.SuspendMode = AppSuspendMode.SLEEP;
    }
    get name() {
      return "AtmosphereMod";
    }
    get icon() {
      return `${"coui://html_ui/efb_ui/efb_apps/AtmoWeather"}/Assets/app-icon.svg`;
    }
    async install(props) {
      bridge.start(props.bus);
    }
    render() {
      return /* @__PURE__ */ import_msfs_sdk3.FSComponent.buildComponent(AtmoWeatherView, { bus: this.bus });
    }
  };
  __name(_AtmoWeather, "AtmoWeather");
  var AtmoWeather = _AtmoWeather;
  Efb.use(AtmoWeather);
})();
