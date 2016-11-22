import { ipcRenderer } from 'electron';
import {
  refreshRiver,
  riverUpdateSuccess,
  riverUpdateFailed,
  riverListUpdateSuccess,
  riverListUpdateFailed,
} from './actions';
import {
  SVR_MSG_LOAD_RIVER,
  CLI_MSG_RIVER_LOAD_FAILURE,
  CLI_MSG_RIVER_LOAD_SUCCESS,

  SVR_MSG_LOAD_RIVER_LIST,
  CLI_MSG_LOAD_RIVER_LIST_SUCCESS,
  CLI_MSG_LOAD_RIVER_LIST_FAILURE,

  SVR_MSG_SET_RIVER_MODE,
} from '../messages';

export function registerMessageHandlers(dispatch) {
  console.log('Starting IPC server...');
  ipcRenderer.on(CLI_MSG_RIVER_LOAD_SUCCESS, (event, args) => {
    dispatch(riverUpdateSuccess(
      args.context.index,
      args.context.river_name,
      args.context.river_url,
      args.context.id,
      args.river
    ));
  });

  ipcRenderer.on(CLI_MSG_RIVER_LOAD_FAILURE, (event, args) => {
    dispatch(riverUpdateFailed(args.context.index, args.error));
  });

  ipcRenderer.on(CLI_MSG_LOAD_RIVER_LIST_SUCCESS, (event, args) => {
    dispatch(riverListUpdateSuccess(args));
    args.rivers.forEach((river, index) => {
      dispatch(refreshRiver(index, river.name, river.url, river.id));
    });
  });

  ipcRenderer.on(CLI_MSG_LOAD_RIVER_LIST_FAILURE, (event, args) => {
    dispatch(riverListUpdateFailed(args));
  });
}

export function sendLoadRiver(index, river_name, river_url, river_id) {
  ipcRenderer.send(SVR_MSG_LOAD_RIVER, {
    river_id: river_id,
    context: {
      id: river_id,
      index: index,
      river_name: river_name,
      river_url: river_url,
    },
  });
}

export function sendLoadRiverList() {
  ipcRenderer.send(SVR_MSG_LOAD_RIVER_LIST, {});
}

export function sendSetRiverMode(river_id, mode) {
  ipcRenderer.send(SVR_MSG_SET_RIVER_MODE, { river_id: river_id, mode: mode });
}
