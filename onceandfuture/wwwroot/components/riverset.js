var React = require('react');
import { connect } from 'react-redux';
import { accountSettingsHide, accountSettingsShow, addRiver, refreshAllFeeds,  } from '../actions';
import {
  SIZE_BUTTON_WIDTH,
  SIZE_BUTTON_HEIGHT,
  SIZE_COLUMN_TOP,
  SIZE_COLUMN_WIDTH,
  SIZE_SPACER_WIDTH,
  SIZE_SPACER_HEIGHT,
  SIZE_PAGE_HEADER,

  Z_INDEX_BANNER,

  APP_BACKGROUND_COLOR,
  APP_TEXT_COLOR,
  BUTTON_STYLE,
  RIVER_TITLE_FONT_SIZE,
  RIVER_TITLE_BACKGROUND_COLOR,
} from './style';
import AccountSettings from './accountsettings';
import River from './river';
import RiverProgress from './riverprogress';
import RiverSetBalloon from './riversetballoon';
import Tooltip from './tooltip';
import UserMenu from './usermenu';

const TITLE_HEIGHT = 33; // <div>"Rivers"..."refresh"</div>

const RiverSetBar = ({title, loading, load_progress, onRefresh, onSettingsClick}) => {
  const div_style = {
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
    height: TITLE_HEIGHT,
  };
  const head_style = {
    fontSize: RIVER_TITLE_FONT_SIZE,
    display: 'inline-block',
    paddingLeft: SIZE_SPACER_WIDTH,
    fontWeight: 'bold',
    paddingTop: 3,
    position: 'relative',
  };

  const refresh_color = loading ? RIVER_TITLE_BACKGROUND_COLOR : APP_TEXT_COLOR;
  const onClick = loading ? () => { } : onRefresh;

  const title_button_style = {
    display: 'inline-block',
    float: 'right',
    verticalAlign: 'middle',
    textAlign: 'center',
  };
  const refresh_style = Object.assign({}, title_button_style, {
    color: refresh_color,
  });

  const announcer_style = {
    display: 'inline-block',
    textAlign: 'center',
    width: '100%',
    verticalAlign: 'middle',
    height: 'auto',
    position: 'relative',
    top: -20,
  };

  return <div>
    <div style={div_style}>
      <div style={head_style}>{title}</div>
      <div style={title_button_style}>
        <UserMenu />
      </div>
      <div style={refresh_style} onClick={onClick} >
        <Tooltip position="bottomleft" tip="Refresh all feeds.">
          <i style={BUTTON_STYLE} onClick={onClick} className="fa fa-refresh" />
        </Tooltip>
      </div>
      <div style={announcer_style}><i>{load_progress.message}</i></div>
    </div>
    <RiverProgress
      progress={load_progress.percent / 100}
      backgroundColor={APP_BACKGROUND_COLOR}
    />
    <RiverSetBalloon />
  </div>;
};

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

export const RiverSetBase = ({
  user,
  rivers,
  loading,
  load_progress,
  show_settings,
  onRefresh,
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
    accountSettings = <AccountSettings />;
    onSettingsClick = onHideSettings;
  }

  return (
    <div>
      <div style={top_bar_style}>
        <RiverSetBar
          title='Rivers'
          loading={loading}
          load_progress={load_progress}
          onRefresh={() => onRefresh(user)}
          onSettingsClick={onSettingsClick}
          />
      </div>
      <div>
        { rivers.map((r, index) => <RiverColumn index={index} key={'r'+index} />) }
        <AddRiverButton index={rivers.length} onAddRiver={() => onAddRiver(user)} />
      </div>

      {accountSettings}
    </div>
  );
};

// VisibleRiverSet
//
const vrs_mapStateToProps = (state) => {
  return {
    user: state.user,
    rivers: state.rivers,
    loading: state.loading,
    load_progress: state.load_progress,
    show_settings: state.account_settings.visible,
  };
};
const vrs_mapDispatchToProps = (dispatch) => {
  return {
    onRefresh: function refreshIt (user) { dispatch(refreshAllFeeds(user)); },
    onAddRiver: function addIt (user) { dispatch(addRiver(user)); },
    onHideSettings: function() { dispatch(accountSettingsHide()); },
    onShowSettings: function() { dispatch(accountSettingsShow()); },
  };
};
const RiverSet = connect(
  vrs_mapStateToProps,
  vrs_mapDispatchToProps
)(
  RiverSetBase
);

export default RiverSet;
