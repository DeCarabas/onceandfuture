import { make_full_url } from './util';
//import { sendLoadRiver, sendLoadRiverList, sendSetRiverMode } from './ipchandler';

export const RIVER_MODE_AUTO = 'auto';
export const RIVER_MODE_IMAGE = 'image';
export const RIVER_MODE_TEXT = 'text';

export const EXPAND_FEED_UPDATE = 'EXPAND_FEED_UPDATE';
export function expandFeedUpdate(river_index, update_key) {
  return {
    type: EXPAND_FEED_UPDATE,
    river_index: river_index,
    update_key: update_key,
  }
}

export const COLLAPSE_FEED_UPDATE = 'COLLAPSE_FEED_UPDATE';
export function collapseFeedUpdate(river_index, update_key) {
  return {
    type: COLLAPSE_FEED_UPDATE,
    river_index: river_index,
    update_key: update_key,
  }
}

export const SHOW_RIVER_SETTINGS = 'SHOW_RIVER_SETTINGS';
export function showRiverSettings(river_index) {
  return {
    type: SHOW_RIVER_SETTINGS,
    river_index: river_index,
  }
}

export const HIDE_RIVER_SETTINGS = 'HIDE_RIVER_SETTINGS';
export function hideRiverSettings(river_index) {
  return {
    type: HIDE_RIVER_SETTINGS,
    river_index: river_index,
  }
}

export const RIVER_ADD_FEED_START = 'RIVER_ADD_FEED_START';
export function riverAddFeedStart(index) {
  return {
    type: RIVER_ADD_FEED_START,
    river_index: index,
  }
}

export const RIVER_ADD_FEED_SUCCESS = 'RIVER_ADD_FEED_SUCCESS';
export function riverAddFeedSuccess(index) {
  return {
    type: RIVER_ADD_FEED_SUCCESS,
    river_index: index,
  }
}

export const RIVER_ADD_FEED_FAILED = 'RIVER_ADD_FEED_FAILED';
export function riverAddFeedFailed(index) {
  return {
    type: RIVER_ADD_FEED_FAILED,
    river_index: index,
  }
}

export const RIVER_ADD_FEED_URL_CHANGED = 'RIVER_ADD_FEED_URL_CHANGED';
export function riverAddFeedUrlChanged(index, new_value) {
  return {
    type: RIVER_ADD_FEED_URL_CHANGED,
    river_index: index,
    new_value: new_value,
  };
}

export const RIVER_LIST_UPDATE_START = 'RIVER_LIST_UPDATE_START';
export function riverListUpdateStart() {
  return {
    type: RIVER_LIST_UPDATE_START,
  };
}

export const RIVER_LIST_UPDATE_SUCCESS = 'RIVER_LIST_UPDATE_SUCCESS';
export function riverListUpdateSuccess(response) {
  return {
    type: RIVER_LIST_UPDATE_SUCCESS,
    response: response,
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
export function refreshAllFeedsProgress(percent) {
  return {
    type: REFRESH_ALL_FEEDS_PROGRESS,
    percent: percent,
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

function xhrAction(options) {
  return function doXHR(dispatch, getState) {
    if (options.precondition) {
      if (!options.precondition(dispatch, getState)) {
        return;
      }
    }

    // TODO: xhr.status has status code

    let xhr = new XMLHttpRequest();
    if (options.start) {
      options.start(dispatch, xhr);
    }
    xhr.open(options.verb || "GET", make_full_url(options.url), true);
    if (options.progress) {
      xhr.addEventListener("progress", () => options.progress(dispatch, xhr));
    }
    if (options.loaded_json) {
      xhr.addEventListener("load", () => {
        let result = JSON.parse(xhr.responseText);
        options.loaded_json(dispatch, result, xhr);
      });
    }
    if (options.loaded) {
      xhr.addEventListener("load", () => options.loaded(dispatch, xhr));
    }
    if (options.error) {
      xhr.addEventListener("error", () => options.error(dispatch, xhr));
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
  }
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

export function addFeedToRiver(index, river) {
  return xhrAction({
    verb: 'POST', url: river.url,
    msg: { 'url': river.modal.value },
    start: (dispatch) => dispatch(riverAddFeedStart(index)),
    loaded: (dispatch, xhr) => {
      dispatch(riverAddFeedSuccess(index));
      dispatch(refreshRiver(index, river.name, river.url, river.id));
    },
    error: (dispatch, xhr) => {
      dispatch(riverAddFeedFailed(index, xhr.statusText));
    },
  });
}

export function refreshRiver(index, river_name, river_url, river_id) {
  return xhrAction({
    url: river_url,
    start: (dispatch) => dispatch(riverUpdateStart(index)),
    loaded_json: (dispatch, result) =>
      dispatch(riverUpdateSuccess(index, river_name, river_url, river_id, result)),
    error: (dispatch, xhr) =>
      dispatch(riverUpdateFailed(index, xhr.statusText)),
  });
}

export function refreshRiverList(user) {
  return xhrAction({
    url: "/api/v1/river/" + user,
    start: (dispatch) => dispatch(riverListUpdateStart()),
    loaded_json: (dispatch, result) => {
      dispatch(riverListUpdateSuccess(result));
      result.rivers.forEach((river, index) => {
        dispatch(refreshRiver(index, river.name, river.url));
      });
    },
    error: (dispatch, xhr) => dispatch(riverListUpdateFailed(xhr.statusText)),
  });
}

export function refreshAllFeeds(user) {
  let pollTimer = null;
  return xhrAction({
    precondition: (dispatch, getState) => {
      const state = getState();
      return !state.loading;
    },
    verb: "POST", url: "/api/v1/river/"+user+"/refresh_all",
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
            let percent = parseInt(part, '10');
            dispatch(refreshAllFeedsProgress(percent));
          }
        }
      }, 100);
      dispatch(refreshAllFeedsStart());
    },
    loaded: (dispatch, xhr) => {
      if (pollTimer) {
        clearInterval(pollTimer);
      }
      dispatch(refreshAllFeedsSuccess());
      dispatch(refreshRiverList(user));
    },
    error: (dispatch, xhr) => {
      if (pollTimer) {
        clearInterval(pollTimer);
      }
      dispatch(refreshAllFeedsError(xhr.statusText));
    },
  })
}
