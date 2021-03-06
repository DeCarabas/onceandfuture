import React from 'react';
import { connect } from 'react-redux';
import { accountSettingsHide, accountSettingsShow, addRiver, } from '../actions';
import {
  SIZE_BUTTON_FONT,
  SIZE_BUTTON_PADDING,
  SIZE_COLUMN_TOP,
  SIZE_COLUMN_WIDTH,
  SIZE_SCREEN_HEIGHT,
  SIZE_SCREEN_WIDTH,
  SIZE_SPACER_WIDTH,
  SIZE_SPACER_HEIGHT,
  SIZE_PAGE_HEADER,

  Z_INDEX_BANNER,
} from './style';
import AccountSettings from './accountsettings';
import AppBanner from './appbanner';
import River from './river';
import Tooltip from './tooltip';

const columnLeft = (index) => index * (SIZE_COLUMN_WIDTH + SIZE_SPACER_WIDTH) + SIZE_SPACER_WIDTH;

export const AddRiverButton = ({index, onAddRiver}) => {
  const column_style = {
    position: 'absolute',
    top: SIZE_COLUMN_TOP + 1,
    left: columnLeft(index),
    padding: SIZE_BUTTON_PADDING,
    cursor: 'pointer',
  };

  return <div style={column_style} onClick={onAddRiver}>
    <Tooltip tip='Add a new river.' position='left'>
      <img src="/round-plus.opt.svg" width={SIZE_BUTTON_FONT} height={SIZE_BUTTON_FONT} />
    </Tooltip>
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
    width: SIZE_SCREEN_WIDTH,
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
    top: 0, left: 0, width: SIZE_SCREEN_WIDTH,
    zIndex: Z_INDEX_BANNER,
    height: SIZE_PAGE_HEADER,
  };

  // This explicitly contains the absolutely-positioned columns within it, so
  // that they're not positioned relative to the body. If they're positioned
  // relative to the body then they stretch out the viewport on mobile, which
  // is no good.
  const river_container_style = {
    position: 'absolute',
    overflowX: 'auto',
    width: SIZE_SCREEN_WIDTH,
    height: SIZE_SCREEN_HEIGHT,
  };

  let accountSettings = <span />;
  let onSettingsClick = onShowSettings;
  if (show_settings) {
    accountSettings = <AccountSettingsContainer />;
    onSettingsClick = onHideSettings;
  }

  const columns = rivers.map((r, i) => <RiverColumn index={i} key={'r'+i} />);
  return <div>
    <div style={top_bar_style}>
      <AppBanner
        title='Rivers'
        load_progress={load_progress}
        onSettingsClick={onSettingsClick}
        />
    </div>
    <div style={river_container_style}>
      <div>{columns}</div>
      <div>
        <AddRiverButton
          index={rivers.length}
          onAddRiver={() => onAddRiver(user)}
        />
      </div>
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
