import React from 'react';
import { connect } from 'react-redux';
import { accountSettingsHide, accountSettingsShow, addRiver, } from '../actions';
import {
  SIZE_BUTTON_WIDTH,
  SIZE_BUTTON_HEIGHT,
  SIZE_COLUMN_TOP,
  SIZE_COLUMN_WIDTH,
  SIZE_SPACER_WIDTH,
  SIZE_SPACER_HEIGHT,
  SIZE_PAGE_HEADER,

  Z_INDEX_BANNER,
} from './style';
import AccountSettings from './accountsettings';
import AppBanner from './appbanner';
import River from './river';

const columnLeft =(index) => index * (SIZE_COLUMN_WIDTH + SIZE_SPACER_WIDTH) + SIZE_SPACER_WIDTH;

export const AddRiverButton = ({index, onAddRiver}) => {
  const column_style = {
    position: 'absolute',
    top: SIZE_COLUMN_TOP,
    left: columnLeft(index),
    width: SIZE_BUTTON_WIDTH,
    height: SIZE_BUTTON_HEIGHT,
    fontSize: SIZE_BUTTON_HEIGHT,

    textAlign: 'center',
    paddingTop: 13,
    cursor: 'pointer',
  };

  return <div style={column_style} onClick={onAddRiver}>
      <i className="fa fa-plus-square" />
  </div>;
};

const RiverColumn = ({index}) => {
  const style = {
    left: columnLeft(index),
    width: SIZE_COLUMN_WIDTH,
    position: 'absolute',
    top: SIZE_COLUMN_TOP,
    bottom: SIZE_SPACER_HEIGHT,
  };
  return <div style={style}>
    <River index={index} />
  </div>;
}

const AccountSettingsContainer = () =>  {
  const style = {
    position: 'absolute',
    top: SIZE_COLUMN_TOP,
    width: '100%',
  };

  return <div style={style}>
    <AccountSettings />
  </div>;
};

const RiverSetBase = ({
  user,
  rivers,
  load_progress,
  show_settings,
  onAddRiver,
  onHideSettings,
  onShowSettings,
}) => {
  const top_bar_style = {
    position: 'fixed',
    top: 0, left: 0, width: '100%',
    zIndex: Z_INDEX_BANNER,
    height: SIZE_PAGE_HEADER,
  };

  let accountSettings = <span />;
  let onSettingsClick = onShowSettings;
  if (show_settings) {
    accountSettings = <AccountSettingsContainer />;
    onSettingsClick = onHideSettings;
  }

  return <div>
    <div style={top_bar_style}>
      <AppBanner
        title='Rivers'
        load_progress={load_progress}
        onSettingsClick={onSettingsClick}
        />
    </div>
    <div>
      {rivers.map((r, i) => <RiverColumn index={i} key={'r'+i} />)}
    </div>
    <div>
      <AddRiverButton
        index={rivers.length}
        onAddRiver={() => onAddRiver(user)}
      />
    </div>
    {accountSettings}
  </div>;
};

const mapStateToProps = (state) => {
  return {
    user: state.user,
    rivers: state.rivers,
    load_progress: state.load_progress,
    show_settings: state.account_settings.visible,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onAddRiver: function addIt (user) { dispatch(addRiver(user)); },
    onHideSettings: function() { dispatch(accountSettingsHide()); },
    onShowSettings: function() { dispatch(accountSettingsShow()); },
  };
};
const RiverSet = connect(mapStateToProps, mapDispatchToProps)(RiverSetBase);

export default RiverSet;
