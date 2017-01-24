import { make_full_url } from './util';

export const RIVER_MODE_AUTO = 'auto';
export const RIVER_MODE_IMAGE = 'image';
export const RIVER_MODE_TEXT = 'text';

export const DROP_RIVER = 'DROP_RIVER';
export function dropRiver(target_index, target_river, dragged_river_id) {
  return {
    type: DROP_RIVER,
    target_index: target_index,
    target_river: target_river,
    dragged_river_id: dragged_river_id,
  };
}

export const EXPAND_FEED_UPDATE = 'EXPAND_FEED_UPDATE';
export function expandFeedUpdate(river_index, update_key) {
  return {
    type: EXPAND_FEED_UPDATE,
    river_index: river_index,
    update_key: update_key,
  };
}

export const COLLAPSE_FEED_UPDATE = 'COLLAPSE_FEED_UPDATE';
export function collapseFeedUpdate(river_index, update_key) {
  return {
    type: COLLAPSE_FEED_UPDATE,
    river_index: river_index,
    update_key: update_key,
  };
}

export const SHOW_RIVER_SETTINGS = 'SHOW_RIVER_SETTINGS';
export function showRiverSettings(river_index) {
  return {
    type: SHOW_RIVER_SETTINGS,
    river_index: river_index,
  };
}

export const HIDE_RIVER_SETTINGS = 'HIDE_RIVER_SETTINGS';
export function hideRiverSettings(river_index) {
  return {
    type: HIDE_RIVER_SETTINGS,
    river_index: river_index,
  };
}

export const RIVER_ADD_FEED_START = 'RIVER_ADD_FEED_START';
export function riverAddFeedStart(index) {
  return {
    type: RIVER_ADD_FEED_START,
    river_index: index,
  };
}

export const RIVER_ADD_FEED_SUCCESS = 'RIVER_ADD_FEED_SUCCESS';
export function riverAddFeedSuccess(index) {
  return {
    type: RIVER_ADD_FEED_SUCCESS,
    river_index: index,
  };
}

export const RIVER_ADD_FEED_FAILED = 'RIVER_ADD_FEED_FAILED';
export function riverAddFeedFailed(index, message) {
  return {
    type: RIVER_ADD_FEED_FAILED,
    error: message,
    river_index: index,
  };
}

export const RIVER_ADD_FEED_FAILED_AMBIGUOUS = 'RIVER_ADD_FEED_FAILED_AMBIGUOUS';
export function riverAddFeedFailedAmbiguous(index, address, feeds) {
  return {
    type: RIVER_ADD_FEED_FAILED_AMBIGUOUS,
    address: address,
    feeds: feeds,
    river_index: index,
  };
}

export const RIVER_LIST_UPDATE_START = 'RIVER_LIST_UPDATE_START';
export function riverListUpdateStart() {
  return {
    type: RIVER_LIST_UPDATE_START,
  };
}

export const RIVER_LIST_UPDATE_SUCCESS = 'RIVER_LIST_UPDATE_SUCCESS';
export function riverListUpdateSuccess(rivers) {
  return {
    type: RIVER_LIST_UPDATE_SUCCESS,
    rivers: rivers,
  };
}

export const RIVER_LIST_UPDATE_FAILED = 'RIVER_LIST_UPDATE_FAILED';
export function riverListUpdateFailed(error) {
  return {
    type: RIVER_LIST_UPDATE_FAILED,
    error: error,
  };
}

export const RIVER_UPDATE_START = 'RIVER_UPDATE_START';
export function riverUpdateStart(index) {
  return {
    type: RIVER_UPDATE_START,
    river_index: index,
  };
}

export const RIVER_UPDATE_SUCCESS = 'RIVER_UPDATE_SUCCESS';
export function riverUpdateSuccess(index, name, url, id, response) {
  return {
    type: RIVER_UPDATE_SUCCESS,
    river_index: index,
    name: name,
    url: url,
    id: id,
    response: response,
  };
}

export const RIVER_UPDATE_FAILED = 'RIVER_UPDATE_FAILED';
export function riverUpdateFailed(index, error) {
  return {
    type: RIVER_UPDATE_FAILED,
    river_index: index,
    error: error,
  };
}

export const REFRESH_ALL_FEEDS_START = 'REFRESH_ALL_FEEDS_START';
export function refreshAllFeedsStart() {
  return {
    type: REFRESH_ALL_FEEDS_START,
  };
}

export const REFRESH_ALL_FEEDS_PROGRESS = 'REFRESH_ALL_FEEDS_PROGRESS';
export function refreshAllFeedsProgress(percent, message) {
  return {
    type: REFRESH_ALL_FEEDS_PROGRESS,
    percent: percent,
    message: message,
  };
}

export const REFRESH_ALL_FEEDS_SUCCESS = 'REFRESH_ALL_FEEDS_SUCCESS';
export function refreshAllFeedsSuccess() {
  return {
    type: REFRESH_ALL_FEEDS_SUCCESS,
  };
}

export const REFRESH_ALL_FEEDS_ERROR = 'REFRESH_ALL_FEEDS_ERROR';
export function refreshAllFeedsError(error) {
  return {
    type: REFRESH_ALL_FEEDS_ERROR,
    error: error,
  };
}

export const ADD_RIVER_START = 'ADD_RIVER_START';
export function addRiverStart() {
  return {
    type: ADD_RIVER_START,
  };
}

export const ADD_RIVER_SUCCESS = 'ADD_RIVER_SUCCESS';
export function addRiverSuccess(rivers, existing_id, existing) {
  return {
    type: ADD_RIVER_SUCCESS,
    rivers: rivers,
    existing: existing,
    existing_id: existing_id,
  };
}

export const ADD_RIVER_ERROR = 'ADD_RIVER_ERROR';
export function addRiverError(error) {
  return {
    type: ADD_RIVER_ERROR,
    error: error,
  };
}

export const REMOVE_RIVER_START = 'REMOVE_RIVER_START';
export function removeRiverStart() {
  return {
    type: REMOVE_RIVER_START,
  };
}

export const REMOVE_RIVER_SUCCESS = 'REMOVE_RIVER_SUCCESS';
export function removeRiverSuccess(user, removed_id, rivers) {
  return {
    type: REMOVE_RIVER_SUCCESS,
    user: user,
    removed_id: removed_id,
    rivers: rivers,
  };
}

export const REMOVE_RIVER_ERROR = 'REMOVE_RIVER_ERROR';
export function removeRiverError(error) {
  return {
    type: REMOVE_RIVER_ERROR,
    error: error,
  };
}

export const DISMISS_BALLOON = 'DISMISS_BALLOON';
export function dismissBalloon() {
  return {
    type: DISMISS_BALLOON,
  };
}

export const DISMISS_RIVER_BALLOON = 'DISMISS_RIVER_BALLOON';
export function dismissRiverBalloon(index) {
  return {
    type: DISMISS_RIVER_BALLOON,
    river_index: index,
  };
}

export const RIVER_GET_FEED_SOURCES_START = 'RIVER_GET_FEED_SOURCES_START';
export function riverGetFeedSourcesStart(index, river) {
  return {
    type: RIVER_GET_FEED_SOURCES_START,
    river_index: index,
    river: river,
  };
}

export const RIVER_GET_FEED_SOURCES_SUCCESS = 'RIVER_GET_FEED_SOURCES_SUCCESS';
export function riverGetFeedSourcesSuccess(index, river, result) {
  return {
    type: RIVER_GET_FEED_SOURCES_SUCCESS,
    river_index: index,
    river: river,
    result: result,
  };
}

export const RIVER_GET_FEED_SOURCES_ERROR = 'RIVER_GET_FEED_SOURCES_ERROR';
export function riverGetFeedSourcesError(index, river, error) {
  return {
    type: RIVER_GET_FEED_SOURCES_ERROR,
    river_index: index,
    river: river,
    error: error,
  };
}

export const RIVER_REMOVE_SOURCE_START = 'RIVER_REMOVE_SOURCE_START';
export function riverRemoveSourceStart(index, river, source_id, source_url) {
  return {
    type: RIVER_REMOVE_SOURCE_START,
    river_index: index,
    river: river,
    source_id: source_id,
    source_url: source_url,
  };
}

export const RIVER_REMOVE_SOURCE_SUCCESS = 'RIVER_REMOVE_SOURCE_SUCCESS';
export function riverRemoveSourceSuccess(index, river, result, source_url) {
  return {
    type: RIVER_REMOVE_SOURCE_SUCCESS,
    river_index: index,
    river: river,
    sources: result.sources,
    source_url: source_url,
  };
}

export const RIVER_REMOVE_SOURCE_ERROR = 'RIVER_REMOVE_SOURCE_ERROR';
export function riverRemoveSourceError(index, river, error) {
  return {
    type: RIVER_REMOVE_SOURCE_ERROR,
    river_index: index,
    river: river,
    error: error,
  };
}

export const RIVER_SET_NAME_START = 'RIVER_SET_NAME_START';
export function riverSetNameStart(index, river, new_name) {
  return {
    type: RIVER_SET_NAME_START,
    river_index: index,
    river: river,
    new_name: new_name,
  };
}

export const RIVER_SET_NAME_SUCCESS = 'RIVER_SET_NAME_SUCCESS';
export function riverSetNameSuccess(index, river, new_name) {
  return {
    type: RIVER_SET_NAME_SUCCESS,
    river_index: index,
    river: river,
    new_name: new_name,
  };
}

export const RIVER_SET_NAME_ERROR = 'RIVER_SET_NAME_ERROR';
export function riverSetNameError(index, river, new_name, error) {
  return {
    type: RIVER_SET_NAME_ERROR,
    river_index: index,
    river: river,
    new_name: new_name,
    error: error,
  };
}

export const ACCOUNT_SETTINGS_TOGGLE = 'ACCOUNT_SETTINGS_TOGGLE';
export function accountSettingsToggle() {
  return function thunk(dispatch, getState) {
    const state = getState();
    const accountSettings = state.account_settings;
    const isSuccess = accountSettings && accountSettings.emailState === 'SUCCESS';
    const isVisible = accountSettings && accountSettings.visible;
    if (!isSuccess && !isVisible) {
      dispatch(getEmail(state.user));
    }

    dispatch({
      type: ACCOUNT_SETTINGS_TOGGLE,
    });
  };
}

export const USER_MENU_TOGGLE = 'USER_MENU_TOGGLE';
export function userMenuToggle() {
  return {
    type: USER_MENU_TOGGLE,
  };
}

export const SIGN_OUT_ERROR = 'SIGN_OUT_ERROR';
export function signOutError(user, error) {
  return {
    type: SIGN_OUT_ERROR,
    user: user,
    error: error,
  };
}

export const GET_EMAIL_START = 'GET_EMAIL_START';
export function getEmailStart() {
  return {
    type: GET_EMAIL_START,
  };
}

export const GET_EMAIL_SUCCESS = 'GET_EMAIL_SUCCESS';
export function getEmailSuccess(email, emailVerified) {
  return {
    type: GET_EMAIL_SUCCESS,
    email: email,
    emailVerified: emailVerified,
  };
}

export const GET_EMAIL_ERROR = 'GET_EMAIL_ERROR';
export function getEmailError(message) {
  return {
    type: GET_EMAIL_ERROR,
    error: message,
  };
}

export const SET_EMAIL_START = 'SET_EMAIL_START';
export function setEmailStart() {
  return {
    type: SET_EMAIL_START,
  };
}

export const SET_EMAIL_SUCCESS = 'SET_EMAIL_SUCCESS';
export function setEmailSuccess(email) {
  return {
    type: SET_EMAIL_SUCCESS,
    email: email,
  };
}

export const SET_EMAIL_ERROR = 'SET_EMAIL_ERROR';
export function setEmailError(message) {
  return {
    type: SET_EMAIL_ERROR,
    error: message,
  };
}

export const SET_PASSWORD_START = 'SET_PASSWORD_START';
export function setPasswordStart() {
  return {
    type: SET_PASSWORD_START,
  };
}

export const SET_PASSWORD_SUCCESS = 'SET_PASSWORD_SUCCESS';
export function setPasswordSuccess() {
  return {
    type: SET_PASSWORD_SUCCESS,
  };
}

export const SET_PASSWORD_ERROR = 'SET_PASSWORD_ERROR';
export function setPasswordError(message) {
  return {
    type: SET_PASSWORD_ERROR,
    error: message,
  };
}

function decodeError(xhr) {
  let  errorMessage = xhr.statusText;
  try
  {
    const fault = JSON.parse(xhr.responseText);
    errorMessage = fault.description || errorMessage;
  }
  catch(_) { /* Ignore errors */ }
  return errorMessage;
}

function xhrAction(options) {
  return function doXHR(dispatch, getState) {
    if (options.precondition) {
      if (!options.precondition(dispatch, getState)) {
        return;
      }
    }

    let xhr = new XMLHttpRequest();
    if (options.start) {
      options.start(dispatch, xhr);
    }
    xhr.open(options.verb || "GET", make_full_url(options.url), true);
    if (options.progress) {
      xhr.addEventListener("progress", () => options.progress(dispatch, xhr));
    }
    if (options.error) {
      xhr.addEventListener("error", () => {
        if (xhr.status == 403 /* Forbidden */) {
          window.location.href = "/login";
        } else {
          options.error(dispatch, decodeError(xhr));
        }
      });
    }
    if (options.loaded_json || options.loaded) {
      xhr.addEventListener("load", () => {
        if (xhr.status == 403 /* Forbidden */) {
          window.location.href = "/login";
        } else if (options.error && xhr.status > 399) {
          options.error(dispatch, decodeError(xhr));
        } else if (options.loaded_json) {
          let result = JSON.parse(xhr.responseText);
          options.loaded_json(dispatch, result, xhr);
        } else {
          options.loaded(dispatch, xhr);
        }
      });
    }
    if (options.abort) {
      xhr.addEventListener("abort", () => options.aborted(dispatch, xhr));
    }
    if (options.msg) {
      xhr.setRequestHeader("content-type", "application/json");
      xhr.send(JSON.stringify(options.msg));
    } else {
      xhr.send();
    }
  };
}

export const RIVER_SET_FEED_MODE = 'RIVER_SET_FEED_MODE';
export function riverSetFeedMode(river_index, river, mode) {
  // Just set the feed mode with the server asynchronously; it's unlikely to
  // fail and it shouldn't take much time so who cares if it fails to land.
  return xhrAction({
    verb: 'POST', url: river.url + '/mode',
    msg: { 'mode': mode },
    start: (dispatch) => dispatch({
      type: RIVER_SET_FEED_MODE,
      river_index: river_index,
      mode: mode,
    }),
  });
}

export function riverAddFeed(index, river, url) {
  return xhrAction({
    verb: 'POST', url: river.url + '/sources',
    msg: { 'url': url },
    start: (dispatch) => dispatch(riverAddFeedStart(index)),
    loaded_json: (dispatch, result) => {
      if (result.status === 'ok') {
        const on_complete = riverAddFeedSuccess(index);
        dispatch(refreshRiver(index, river.name, river.url, river.id, on_complete));
      } else if (result.status == 'ambiguous') {
        dispatch(riverAddFeedFailedAmbiguous(index, url, result.feeds));
      } else {
        dispatch(riverAddFeedFailed(index, "an unexpected server response."));
      }
    },
    error: (dispatch, message) => {
      dispatch(riverAddFeedFailed(index, message));
    },
  });
}

export function addRiver(user, id = null) {
  return xhrAction({
    verb: 'POST', url: "/api/v1/user/" + user,
    msg: { name: null, id: id },
    start: (dispatch) => dispatch(addRiverStart()),
    loaded_json: (dispatch, result) => {
      dispatch(addRiverSuccess(result.rivers, id, result.existing));
    },
    error: (dispatch, message) => {
      dispatch(addRiverError(message));
    },
  });
}

export function removeRiver(user, river) {
  return xhrAction({
    verb: 'DELETE', url: river.url,
    start: (dispatch) => dispatch(removeRiverStart()),
    loaded_json: (dispatch, result) => {
      dispatch(removeRiverSuccess(user, river.id, result.rivers));
    },
    error: (dispatch, message) => {
      dispatch(removeRiverError(message));
    },
  });
}

export function refreshRiver(index, river_name, river_url, river_id, on_complete) {
  return xhrAction({
    url: river_url,
    start: (dispatch) => dispatch(riverUpdateStart(index)),
    loaded_json: (dispatch, result) => {
      dispatch(riverUpdateSuccess(index, river_name, river_url, river_id, result));
      if (on_complete) {
        dispatch(on_complete);
      }
    },
    error: (dispatch, message) => {
      dispatch(riverUpdateFailed(index, message));
      if (on_complete) {
        dispatch(on_complete);
      }
    },
  });
}

export function refreshRiverList(user) {
  return xhrAction({
    url: "/api/v1/user/" + user,
    start: (dispatch) => dispatch(riverListUpdateStart()),
    loaded_json: (dispatch, result) => {
      dispatch(riverListUpdateSuccess(result.rivers));
      result.rivers.forEach((river, index) => {
        dispatch(refreshRiver(index, river.name, river.url, river.id));
      });
    },
    error: (dispatch, message) => dispatch(riverListUpdateFailed(message)),
  });
}

export function refreshAllFeeds(user) {
  let pollTimer = null;
  return xhrAction({
    precondition: (dispatch, getState) => {
      const state = getState();
      return !state.loading;
    },
    verb: "POST", url: "/api/v1/user/"+user+"/refresh_all",
    start: (dispatch, xhr) => {
      pollTimer = setInterval(() => {
        let text = xhr.responseText;
        if (text) {
          let lines = text.split('\n');
          let part = lines[lines.length - 1];
          if (part.length === 0 && lines.length > 1) {
            part = lines[lines.length - 2];
          }
          if (part.length > 0) {
            const subParts = part.split('|');
            const percent = parseInt(subParts[0], '10');
            const message = subParts.length > 1 ? subParts[1] : '';
            dispatch(refreshAllFeedsProgress(percent, message));
          }
        }
      }, 100);
      dispatch(refreshAllFeedsStart());
    },
    loaded: (dispatch) => {
      if (pollTimer) {
        clearInterval(pollTimer);
      }
      dispatch(refreshAllFeedsSuccess());
      dispatch(refreshRiverList(user));
    },
    error: (dispatch, message) => {
      if (pollTimer) {
        clearInterval(pollTimer);
      }
      dispatch(refreshAllFeedsError(message));
    },
  });
}

export function riverGetFeedSources(index, river) {
  return xhrAction({
    url: river.url + '/sources',
    start: (dispatch) => {
      dispatch(riverGetFeedSourcesStart(index, river));
    },
    loaded_json: (dispatch, result) => {
      dispatch(riverGetFeedSourcesSuccess(index, river, result));
    },
    error: (dispatch, message) => {
      dispatch(riverGetFeedSourcesError(index, river, message));
    },
  });
}

export function riverRemoveSource(index, river, source_id, source_url) {
  return xhrAction({
    verb: 'DELETE', url: river.url + '/sources/' + source_id,
    start: (dispatch) => {
      dispatch(riverRemoveSourceStart(index, river, source_id, source_url));
    },
    loaded_json: (dispatch, result) => {
      const on_refresh_complete = riverRemoveSourceSuccess(index, river, result, source_url);
      dispatch(refreshRiver(index, river.name, river.url, river.id, on_refresh_complete));
    },
    error: (dispatch, message) => {
      dispatch(riverRemoveSourceError(index, river, message));
    },
  });
}

export function riverSetName(index, river, new_name) {
  return xhrAction({
    verb: 'PUT', url: river.url + '/name',
    msg: { name: new_name },
    start: (dispatch) => {
      dispatch(riverSetNameStart(index, river, new_name));
    },
    loaded: (dispatch) => {
      dispatch(riverSetNameSuccess(index, river, new_name));
    },
    error: (dispatch, message) => {
      dispatch(riverSetNameError(index, river, new_name, message));
    },
  });
}

export function setRiverOrder(user, river_order) {
  return xhrAction({
    verb: 'POST', url: '/api/v1/user/' + user + '/set_order',
    msg: { riverIds: river_order },
  });
}

export function signOut(user) {
  return xhrAction({
    verb: 'POST', url: '/api/v1/user/' + user +'/signout',
    msg: {},
    loaded: () => {
      window.location.href = "/";
    },
    error: (dispatch, message) => {
      dispatch(signOutError(user, message));
    },
  });
}

export function getEmail(user) {
  return xhrAction({
    url: '/api/v1/user/' + user + '/email',
    start: (dispatch) => {
      dispatch(getEmailStart());
    },
    loaded_json: (dispatch, result) => {
      dispatch(getEmailSuccess(result.email, result.emailVerified));
    },
    error: (dispatch, message) => {
      dispatch(getEmailError(message));
    },
  });
}

export function setEmail(user, email) {
  return xhrAction({
    verb: 'POST', url: '/api/v1/user/' + user + '/email',
    msg: { 'email': email },
    start: (dispatch) => dispatch(setEmailStart()),
    loaded: (dispatch) => dispatch(setEmailSuccess(email)),
    error: (dispatch, message) => dispatch(setEmailError(message)),
  });
}

export function setPassword(user, password) {
  return xhrAction({
    verb: 'POST', url: '/api/v1/user/' + user + '/password',
    msg: { 'password': password },
    start: (dispatch) => dispatch(setPasswordStart()),
    loaded: (dispatch) => dispatch(setPasswordSuccess()),
    error: (dispatch, message) => dispatch(setPasswordError(message)),
  });
}
