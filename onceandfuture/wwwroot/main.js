var React = require('react');
var ReactDOM = require('react-dom');
import { Provider } from 'react-redux';
import { createStore, applyMiddleware } from 'redux';
import thunkMiddleware from 'redux-thunk';
import createLogger from 'redux-logger';
import { update_key } from './util';

import {
  RIVER_MODE_AUTO,

  ACCOUNT_SETTINGS_TOGGLE,

  ADD_RIVER_ERROR,
  ADD_RIVER_START,
  ADD_RIVER_SUCCESS,
  DISMISS_BALLOON,
  DISMISS_RIVER_BALLOON,
  DROP_RIVER,
  EXPAND_FEED_UPDATE,
  COLLAPSE_FEED_UPDATE,
  SHOW_RIVER_SETTINGS,
  HIDE_RIVER_SETTINGS,
  REMOVE_RIVER_ERROR,
  REMOVE_RIVER_START,
  REMOVE_RIVER_SUCCESS,
  RIVER_ADD_FEED_START,
  RIVER_ADD_FEED_FAILED,
  RIVER_ADD_FEED_SUCCESS,
  RIVER_GET_FEED_SOURCES_ERROR,
  RIVER_GET_FEED_SOURCES_START,
  RIVER_GET_FEED_SOURCES_SUCCESS,
  RIVER_LIST_UPDATE_FAILED,
  RIVER_LIST_UPDATE_START,
  RIVER_LIST_UPDATE_SUCCESS,
  RIVER_REMOVE_SOURCE_ERROR,
  RIVER_REMOVE_SOURCE_START,
  RIVER_REMOVE_SOURCE_SUCCESS,
  RIVER_SET_FEED_MODE,
  RIVER_SET_NAME_ERROR,
  RIVER_SET_NAME_SUCCESS,
  RIVER_UPDATE_START,
  RIVER_UPDATE_FAILED,
  RIVER_UPDATE_SUCCESS,
  REFRESH_ALL_FEEDS_START,
  REFRESH_ALL_FEEDS_SUCCESS,
  REFRESH_ALL_FEEDS_ERROR,
  REFRESH_ALL_FEEDS_PROGRESS,
  USER_MENU_TOGGLE,

  addRiver,
  refreshRiverList,
  riverAddFeed,
  setRiverOrder,
} from './actions';

import AppRoot from './components/approot';

// User doesn't channge, based on host URL.
const user = window.location.pathname.split('/')[2];

// The redux reducer-- this is the core logic of the app that evolves the app
// state in response to actions.

// The default state of a river object.
const def_river = {
  modal: { kind: 'none', },
  name: '(Untitled)',
  updates: [],
  url: '',
  mode: RIVER_MODE_AUTO,
};

function apply_state_array(state, index, reduce, action) {
  if (index === undefined) { return state; }
  return index < 0
    ? state
    : [].concat(
        state.slice(0, index),
        reduce(state[index], action),
        state.slice(index+1, state.length)
      );
}

function migrate_updates(old_updates, new_updates) {
  // We grow updates at the top.
  if (!old_updates.length) { return new_updates; }
  const start_key = update_key(old_updates[0]);
  const match_index = new_updates.findIndex(u => update_key(u) === start_key);
  if (match_index < 0) { return new_updates; }
  return [].concat(new_updates.slice(0, match_index), old_updates);
}

function state_river_feed_update(state = {}, action) {
  switch(action.type) {
    case EXPAND_FEED_UPDATE:
      return Object.assign({}, state, { expanded: true });
    case COLLAPSE_FEED_UPDATE:
      return Object.assign({}, state, { expanded: false });
    default:
      return state;
  }
}

function state_river_feed_updates(state = [], action) {
  switch(action.type) {
    case RIVER_UPDATE_SUCCESS:
      return migrate_updates(state, action.response.updatedFeeds.updatedFeed);
    case EXPAND_FEED_UPDATE:
    case COLLAPSE_FEED_UPDATE:
      const index = state.findIndex(u => update_key(u) === action.update_key);
      return apply_state_array(state, index, state_river_feed_update, action);
    default:
      return state;
  }
}

function get_river_info(action) {
  let errorDetail = action.error ? "  The error was " + action.error : "";
  switch(action.type) {
    case RIVER_UPDATE_FAILED:
      return {
        text: "I can't update this river right now.",
        level: 'error',
      };

    case RIVER_ADD_FEED_FAILED:
      return {
        text: "I can't add that feed to this river right now." + errorDetail,
        level: 'error',
      };

    case RIVER_REMOVE_SOURCE_ERROR:
      return {
        text: "I can't remove that feed right now." + errorDetail,
        level: 'error',
      };

    default:
      return {};
  }
}


function state_river(state = def_river, action) {
  switch(action.type) {
    case DISMISS_RIVER_BALLOON:
      return Object.assign({}, state, {
        modal: { kind: 'none', },
      });

    case RIVER_ADD_FEED_START:
    case RIVER_UPDATE_START:
    case RIVER_REMOVE_SOURCE_START:
      return Object.assign({}, state, {
        modal: { kind: 'loading', percent: 1 },
      });

    case RIVER_SET_FEED_MODE:
      return Object.assign({}, state, {
        mode: action.mode,
      });

    case RIVER_ADD_FEED_SUCCESS:
      return Object.assign({}, state, {
        modal: { kind: 'none', },
        sources: null,
      });

    case RIVER_ADD_FEED_FAILED:
    case RIVER_UPDATE_FAILED:
    case RIVER_REMOVE_SOURCE_ERROR:
    case RIVER_SET_NAME_ERROR:
      return Object.assign({}, state, {
        modal: { kind: 'bubble', info: get_river_info(action), },
      });

    case RIVER_UPDATE_SUCCESS:
      return Object.assign({}, state, {
        modal: { kind: 'none', },
        name: action.name,
        updates: state_river_feed_updates(state.updates, action),
        feeds: action.feeds,
        url: action.url,
        id: action.id,
        mode: action.response.metadata.mode || state.mode,
        sources: null,
      });

    case SHOW_RIVER_SETTINGS:
      return Object.assign({}, state, {
        modal: { kind: 'settings', value: '', },
      });

    case HIDE_RIVER_SETTINGS:
      return Object.assign({}, state, {
        modal: { kind: 'none', },
      });

    case EXPAND_FEED_UPDATE:
    case COLLAPSE_FEED_UPDATE:
      return Object.assign({}, state, {
        updates: state_river_feed_updates(state.updates, action),
      });

    case RIVER_GET_FEED_SOURCES_ERROR:
      return Object.assign({}, state, {
        sources: 'ERROR',
      });

    case RIVER_GET_FEED_SOURCES_SUCCESS:
      return Object.assign({}, state, {
        sources: action.result.sources,
      });

    case RIVER_GET_FEED_SOURCES_START:
      return Object.assign({}, state, {
        sources: 'PENDING',
      });

    case RIVER_SET_NAME_SUCCESS:
      return Object.assign({}, state, {
        name: action.new_name,
      });

    case RIVER_REMOVE_SOURCE_SUCCESS:
      return Object.assign({}, state, {
        sources: action.sources,
        modal: { kind: 'bubble', info: {
          text: "Feed removed.",
          action: riverAddFeed(action.river_index, action.river, action.source_url),
          action_label: "undo",
          level: 'info',
        }},
      });

    default:
      return state;
  }
}

function state_rivers(state = [], action) {
  switch(action.type) {
    case ADD_RIVER_SUCCESS:
    case REMOVE_RIVER_SUCCESS:
    case RIVER_LIST_UPDATE_SUCCESS:
      return action.rivers.map(nr => {
        let old_river = state.find(or => or.name === nr.name) || def_river;
        return Object.assign({}, old_river, {
          name: nr.name,
          url: nr.url,
          id: nr.id,
        });
      });

    case DROP_RIVER:
      const source_river_index = state.findIndex((r) => r.id === action.dragged_river_id);
      if (source_river_index < 0) { return state; }

      // Leave out the source river...
      const state_without_source = [].concat(
        state.slice(0, source_river_index),
        state.slice(source_river_index + 1, state.length)
      );

      // And splice it to the left of the target index.
      return [].concat(
        state_without_source.slice(0, action.target_index),
        [ state[source_river_index] ],
        state_without_source.slice(action.target_index, state_without_source.length)
      );

    default: // By default forward events to the appropriate element.
      return apply_state_array(state, action.river_index, state_river, action);
  }
}

function state_loading(state = false, action) {
  switch(action.type) {
    case REFRESH_ALL_FEEDS_START:
      return true;

    case REFRESH_ALL_FEEDS_SUCCESS:
    case REFRESH_ALL_FEEDS_ERROR:
      return false;

    default:
      return state;
  }
}

const DEFAULT_PROGRESS={ percent: 0, message: '' };
function state_load_progress(state = DEFAULT_PROGRESS, action) {
  switch(action.type) {
    case REFRESH_ALL_FEEDS_START:
    case REFRESH_ALL_FEEDS_SUCCESS:
    case REFRESH_ALL_FEEDS_ERROR:
      return { percent: 0, message: '' };

    case REFRESH_ALL_FEEDS_PROGRESS:
      return { percent: action.percent, message: action.message };

    default:
      return state;
  }
}

function state_top_info(state = {}, action) {
  switch(action.type) {
    case DISMISS_BALLOON:
      return {};

    // In-progress cases, clear the balloon.
    case ADD_RIVER_START:
    case REFRESH_ALL_FEEDS_START:
    case REMOVE_RIVER_START:
    case RIVER_LIST_UPDATE_START:
    case RIVER_UPDATE_START:
      return {};

    // Error cases.
    case ADD_RIVER_ERROR:
      return {
        text: "I can't add a river right now.",
        level: 'error',
      };

    case REFRESH_ALL_FEEDS_ERROR:
      return {
        text: "I can't refresh your feeds right now.",
        level: 'error',
      };

    case REMOVE_RIVER_ERROR:
      return {
        text: "I can't remove the river right now.",
        level: 'error',
      };

    case RIVER_LIST_UPDATE_FAILED:
      return {
        text: "I can't update your list of rivers right now.",
        level: 'error',
      };

    // Undo cases.
    case REMOVE_RIVER_SUCCESS:
      return {
        text: "River removed.",
        action: addRiver(action.user, action.removed_id),
        action_label: "undo",
        level: 'info',
      };

    default:
      return state;
  }
}

function state_account_settings(state = {visible: false}, action) {
  switch(action.type)
  {
    case ACCOUNT_SETTINGS_TOGGLE:
      return Object.assign({}, state, {
        visible: !state.visible,
      });
    default:
      return state;
  }
}

function state_user_menu(state = {visible: false}, action) {
  switch(action.type)
  {
    case USER_MENU_TOGGLE:
      return Object.assign({}, state, {
        visible: !state.visible,
      });
    default:
      return state;
  }
}

function sociallistsApp(state = {}, action) {
  return {
    user: user,
    rivers: state_rivers(state.rivers, action),
    loading: state_loading(state.loading, action),
    load_progress: state_load_progress(state.load_progress, action),
    top_info: state_top_info(state.top_info, action),
    account_settings: state_account_settings(state.account_settings, action),
    user_menu: state_user_menu(state.user_menu, action),
  };
}

// State store, where it all comes together.
//
const logger = createLogger({
  collapsed: true,
});
const store = createStore(
  sociallistsApp,
  applyMiddleware(thunkMiddleware, logger)
);

/////// COLUMN ORDER TRACKING DIRTY HACK.

function arrayEqual(a1, a2) {
  return a1.length == a2.length && a1.every((v,i) => v === a2[i]);
}

let last_column_order = null;
store.subscribe(() => {
  const state = store.getState();
  if (state.rivers && state.rivers.length > 1) {
    const new_column_order = state.rivers.map(r => r.id);
    if (last_column_order != null && !arrayEqual(new_column_order, last_column_order)) {
      store.dispatch(setRiverOrder(user, new_column_order));
    }
    last_column_order = new_column_order;
  }
});

ReactDOM.render(
  <Provider store={store}>
    <AppRoot />
  </Provider>,
  document.getElementById('example')
);

store.dispatch(refreshRiverList(user));
